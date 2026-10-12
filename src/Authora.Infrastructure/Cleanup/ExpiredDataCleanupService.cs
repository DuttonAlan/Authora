// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Runs the <see cref="ExpiredDataCleaner"/> every <see cref="CleanupOptions.Interval"/>, starting one interval
///     after startup.
/// </summary>
/// <param name="scopeFactory">Creates a scope with its own DbContext per run.</param>
/// <param name="options">The interval.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record failed runs.</param>
public sealed class ExpiredDataCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredDataCleanupService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleaner = scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>();

                await cleaner.CleanAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run, e.g. while the database is unavailable, must not stop the host.
                logger.CleanupFailed(exception);
            }
        }
    }
}
