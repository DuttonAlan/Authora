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
///         attempts it is dropped and recorded as <see cref="AuditEventTypes.Clients.BackchannelLogoutFailed"/>. A
///         notification older than <see cref="BackchannelLogoutOptions.MaxAge"/>, or for a URI that does not use HTTPS
///         while <see cref="BackchannelLogoutOptions.AllowHttp"/> is off, is given up the same way without being sent.
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

    private enum OutcomeKind
    {
        Delivered,
        Failed,
        GivenUp,
        Dropped
    }

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
        var outcomes = await Task.WhenAll(claimed.Select(notification =>
            DeliverAsync(notification, uris.GetValueOrDefault(notification.ClientId), now, settings, cancellationToken)));

        for (var i = 0; i < claimed.Count; i++)
        {
            HandleOutcome(claimed[i], outcomes[i], now, settings);
        }

        await context.SaveChangesAsync(cancellationToken);

        return claimed.Count;
    }

    /// <summary>
    ///     Checks whether a notification may still be sent, and sends it.
    /// </summary>
    private async Task<Outcome> DeliverAsync(
        BackchannelLogoutNotification notification,
        string? uri,
        DateTime now,
        BackchannelLogoutOptions settings,
        CancellationToken cancellationToken)
    {
        if (uri is null)
        {
            return Outcome.Dropped;
        }

        // Checked before sending, so a notification that waited too long, e.g. during an outage, is never sent late.
        if (now - notification.CreatedAt >= settings.MaxAge)
        {
            return Outcome.GiveUp(notification.LastError is null
                ? $"Older than {settings.MaxAge}"
                : $"Older than {settings.MaxAge}, last error: {notification.LastError}");
        }

        // Checked again, not only when the URI was saved: an HTTP URI saved while HTTP was allowed, e.g. in a database
        // copied from development, must not receive Logout Tokens in plain text once that is no longer allowed.
        if (!IsAllowedScheme(uri, settings))
        {
            return Outcome.GiveUp("The Back-Channel Logout URI does not use HTTPS");
        }

        var error = await SendAsync(notification, uri, now, cancellationToken);

        return error is null ? Outcome.Delivered : Outcome.Failed(error);
    }

    private void HandleOutcome(BackchannelLogoutNotification notification, Outcome outcome, DateTime now, BackchannelLogoutOptions settings)
    {
        switch (outcome.Kind)
        {
            case OutcomeKind.Dropped:
                context.BackchannelLogoutNotifications.Remove(notification);
                logger.LogoutTokenDropped(notification.Id, notification.ClientId);
                return;

            case OutcomeKind.Delivered:
                context.BackchannelLogoutNotifications.Remove(notification);
                logger.LogoutTokenDelivered(notification.Id, notification.UserId, notification.ClientId);
                return;

            case OutcomeKind.GivenUp:
                GiveUp(notification, notification.Attempts, outcome.Error!);
                return;

            case OutcomeKind.Failed:
            default:
                break;
        }

        var attempts = notification.Attempts + 1;

        if (attempts >= settings.MaxAttempts)
        {
            GiveUp(notification, attempts, outcome.Error!);
            return;
        }

        var nextAttemptAt = now + BackchannelLogoutOptions.GetRetryDelay(attempts);

        notification.RecordFailure(outcome.Error!, nextAttemptAt);
        logger.LogoutTokenRetryScheduled(notification.Id, notification.ClientId, attempts, outcome.Error!, nextAttemptAt);
    }

    private void GiveUp(BackchannelLogoutNotification notification, int attempts, string error)
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
    }

    private static bool IsAllowedScheme(string uri, BackchannelLogoutOptions settings)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || (settings.AllowHttp && parsed.Scheme == Uri.UriSchemeHttp));
    }

    /// <summary>
    ///     Sends the Logout Token of a notification.
    /// </summary>
    /// <returns>Null if the Client accepted it, otherwise why not. Never contains the token.</returns>
    private async Task<string?> SendAsync(
        BackchannelLogoutNotification notification,
        string uri,
        DateTime now,
        CancellationToken cancellationToken)
    {
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

    /// <summary>
    ///     What happened to a notification in this run.
    /// </summary>
    /// <param name="Kind">Delivered, failed (retried unless it was the last attempt), given up without sending, or dropped.</param>
    /// <param name="Error">Why it failed or was given up, never the token.</param>
    private sealed record Outcome(OutcomeKind Kind, string? Error = null)
    {
        public static readonly Outcome Delivered = new(OutcomeKind.Delivered);

        public static readonly Outcome Dropped = new(OutcomeKind.Dropped);

        public static Outcome Failed(string error) => new(OutcomeKind.Failed, error);

        public static Outcome GiveUp(string error) => new(OutcomeKind.GivenUp, error);
    }
}
