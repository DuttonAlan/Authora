// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Oidc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Reloads the signing keys every <see cref="OidcOptions.KeyReloadInterval"/>, so keys can be rotated without a
///     restart (US-035), and records every change of the keys in the audit log.
/// </summary>
/// <remarks>
///     The keys are compared with the last <see cref="AuditEventTypes.System.SigningKeysChanged"/> event at startup
///     too, so a rotation by restart is recorded as well. Several instances record the same change only once, unless
///     they check at the same moment.
/// </remarks>
/// <param name="provider">The provider of the keys.</param>
/// <param name="scopeFactory">Creates a scope with its own DbContext for the audit log.</param>
/// <param name="options">The reload interval.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record failed reloads.</param>
public sealed class SigningKeyReloadService(
    PemSigningKeyProvider provider,
    IServiceScopeFactory scopeFactory,
    IOptions<OidcOptions> options,
    TimeProvider timeProvider,
    ILogger<SigningKeyReloadService> logger) : BackgroundService
{
    private string? _recordedKeys;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecordIfChangedAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.Value.KeyReloadInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                provider.Reload();
            }
            catch (Exception exception) when (exception is InvalidOperationException or OptionsValidationException)
            {
                logger.SigningKeysReloadFailed(exception);
            }

            await RecordIfChangedAsync(stoppingToken);
        }
    }

    private async Task RecordIfChangedAsync(CancellationToken cancellationToken)
    {
        var signingKeyId = provider.SigningKey.KeyId;
        var details = $"Published: {string.Join(", ", provider.PublishedKeys.Select(key => key.KeyId))}";
        var keys = $"{signingKeyId} {details}";

        if (keys == _recordedKeys)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var auditEventRepository = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();

            var latest = await auditEventRepository.GetLatest(AuditEventTypes.System.SigningKeysChanged, cancellationToken);

            if (latest?.TargetId != signingKeyId || latest.Details != details)
            {
                await scope.ServiceProvider.GetRequiredService<IAuditLog>().RecordNowAsync(
                    new AuditEntry(AuditEventTypes.System.SigningKeysChanged, AuditTargetTypes.SigningKey, signingKeyId, details),
                    cancellationToken);
            }

            _recordedKeys = keys;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // E.g. the database is unavailable. The keys are in use anyway, only the record is missing for now.
            logger.SigningKeysAuditFailed(exception);
        }
    }
}
