// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.BackchannelLogout;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;
using Authora.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.BackchannelLogout;

/// <summary>
///     Sends the due back-channel logout notifications of the outbox: a <c>POST</c> of the Logout Token as
///     <c>application/x-www-form-urlencoded</c> parameter <c>logout_token</c> to the Back-Channel Logout URI of the
///     Client (OpenID Connect Back-Channel Logout 1.0, Section 2.5). A 2xx response counts as delivered.
/// </summary>
/// <remarks>
///     <para>
///         Each run claims up to <see cref="BackchannelLogoutOptions.BatchSize"/> due notifications by moving their next
///         attempt to the end of <see cref="BackchannelLogoutOptions.ClaimDuration"/>, skipping rows another instance is
///         claiming, so several instances never send the same notification at the same time. If an instance stops while
///         sending, another one takes the notification over once the claim ended.
///     </para>
///     <para>
///         A failed delivery is retried with a delay that doubles each time
///         (<see cref="BackchannelLogoutOptions.GetRetryDelay"/>). After <see cref="BackchannelLogoutOptions.MaxAttempts"/>
///         attempts, or once the notification is older than <see cref="BackchannelLogoutOptions.MaxAge"/>, it is dropped
///         and recorded as <see cref="AuditEventTypes.Clients.BackchannelLogoutFailed"/>.
///     </para>
/// </remarks>
/// <param name="context">The DbContext of the outbox.</param>
/// <param name="logoutTokenIssuer">Creates the Logout Tokens.</param>
/// <param name="httpClientFactory">Creates the HTTP client <see cref="HttpClientName"/>.</param>
/// <param name="auditLog">The audit log to record notifications that were given up.</param>
/// <param name="options">The back-channel logout settings.</param>
/// <param name="logger">The Logger to record deliveries.</param>
public sealed class BackchannelLogoutDeliverer(
    AuthoraContext context,
    LogoutTokenIssuer logoutTokenIssuer,
    IHttpClientFactory httpClientFactory,
    IAuditLog auditLog,
    IOptions<BackchannelLogoutOptions> options,
    ILogger<BackchannelLogoutDeliverer> logger)
{
    /// <summary>
    ///     Name of the HTTP client that sends the Logout Tokens: <see cref="BackchannelLogoutOptions.Timeout"/>, no
    ///     automatic redirects.
    /// </summary>
    public const string HttpClientName = "Authora.BackchannelLogout";

    /// <summary>
    ///     Claims and sends one batch of due notifications.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the run.</param>
    /// <returns>The number of notifications claimed; less than the batch size if no more are due.</returns>
    public async Task<int> DeliverDueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        // Tracked, so the outcome of each delivery is saved with one SaveChanges below.
        var claimed = await context.BackchannelLogoutNotifications
            .FromSqlRaw(BackchannelLogoutNotificationConfiguration.ClaimDueSql, now, settings.BatchSize, now + settings.ClaimDuration)
            .ToListAsync(cancellationToken);

        if (claimed.Count == 0)
        {
            return 0;
        }

        var clientIds = claimed.Select(notification => notification.ClientId).Distinct().ToList();

        // The current URI: an administrator may have changed or removed it since the Session ended.
        var uris = await context.Clients
            .AsNoTracking()
            .Where(client => clientIds.Contains(client.Id))
            .Select(client => new { client.Id, client.BackchannelLogoutUri })
            .ToDictionaryAsync(client => client.Id, client => client.BackchannelLogoutUri, cancellationToken);

        // Sent at the same time, so one slow Client does not hold up the others for the whole batch.
        var errors = await Task.WhenAll(claimed.Select(notification =>
            SendAsync(notification, uris.GetValueOrDefault(notification.ClientId), now, cancellationToken)));

        for (var i = 0; i < claimed.Count; i++)
        {
            HandleOutcome(claimed[i], uris.GetValueOrDefault(claimed[i].ClientId), errors[i], now, settings);
        }

        await context.SaveChangesAsync(cancellationToken);

        return claimed.Count;
    }

    private void HandleOutcome(
        BackchannelLogoutNotification notification,
        string? uri,
        string? error,
        DateTime now,
        BackchannelLogoutOptions settings)
    {
        if (uri is null)
        {
            context.BackchannelLogoutNotifications.Remove(notification);
            logger.LogoutTokenDropped(notification.Id, notification.ClientId);
            return;
        }

        if (error is null)
        {
            context.BackchannelLogoutNotifications.Remove(notification);
            logger.LogoutTokenDelivered(notification.Id, notification.UserId, notification.ClientId);
            return;
        }

        var attempts = notification.Attempts + 1;

        if (attempts >= settings.MaxAttempts || now - notification.CreatedAt >= settings.MaxAge)
        {
            context.BackchannelLogoutNotifications.Remove(notification);
            logger.LogoutTokenGivenUp(notification.Id, notification.UserId, notification.ClientId, attempts, error);

            // Staged in this context, so it is saved together with the removal.
            auditLog.Record(AuditEntry.ForClient(
                AuditEventTypes.Clients.BackchannelLogoutFailed,
                notification.ClientId,
                $"User {notification.UserId}, {attempts} attempts, last error: {error}") with
            {
                Outcome = AuditOutcome.Failed,
                Actor = AuditActor.System
            });

            return;
        }

        var nextAttemptAt = now + BackchannelLogoutOptions.GetRetryDelay(attempts);

        notification.RecordFailure(error, nextAttemptAt);
        logger.LogoutTokenRetryScheduled(notification.Id, notification.ClientId, attempts, error, nextAttemptAt);
    }

    /// <summary>
    ///     Sends the Logout Token of a notification.
    /// </summary>
    /// <returns>Null if the Client accepted it, otherwise why not. Never contains the token.</returns>
    private async Task<string?> SendAsync(
        BackchannelLogoutNotification notification,
        string? uri,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (uri is null)
        {
            return null;
        }

        using var content = new FormUrlEncodedContent([new("logout_token", logoutTokenIssuer.Issue(notification, now))]);
        using var http = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await http.PostAsync(new Uri(uri, UriKind.Absolute), content, cancellationToken);

            // Section 2.8: 200 OK on success; any other 2xx is accepted too. A redirect is not followed.
            return response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
        }
        catch (HttpRequestException exception)
        {
            return exception.Message;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"No response within {options.Value.Timeout}";
        }
    }
}
