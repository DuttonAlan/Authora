// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.BackchannelLogout;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.BackchannelLogout;

/// <summary>
///     Runs the <see cref="BackchannelLogoutDeliverer"/> every <see cref="BackchannelLogoutOptions.PollInterval"/>,
///     batch after batch until no notification is due. Runs on every instance; the claims keep them from sending the
///     same notification.
/// </summary>
/// <param name="scopeFactory">Creates a scope with its own DbContext per batch.</param>
/// <param name="options">The poll interval and batch size.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record failed runs.</param>
public sealed class BackchannelLogoutDeliveryService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackchannelLogoutOptions> options,
    TimeProvider timeProvider,
    ILogger<BackchannelLogoutDeliveryService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                int claimed;

                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var deliverer = scope.ServiceProvider.GetRequiredService<BackchannelLogoutDeliverer>();

                    claimed = await deliverer.DeliverDueAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
                }
                while (claimed == options.Value.BatchSize);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run, e.g. while the database is unavailable, must not stop the host. Claimed notifications
                // are taken over again once their claim ended.
                logger.DeliveryFailed(exception);
            }
        }
    }
}
