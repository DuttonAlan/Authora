// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit.Chain;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Auditing;

/// <summary>
///     Runs the <see cref="AuditChainSealer"/> every <see cref="AuditChainOptions.SealInterval"/>, starting one
///     interval after startup.
/// </summary>
/// <param name="scopeFactory">Creates a scope with its own DbContext per run.</param>
/// <param name="options">The interval.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record failed runs.</param>
public sealed class AuditChainSealingService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditChainOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditChainSealingService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SealInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sealer = scope.ServiceProvider.GetRequiredService<AuditChainSealer>();

                await sealer.SealAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run, e.g. while the database is unavailable, must not stop the host. The events are sealed
                // by the next run.
                logger.SealingFailed(exception);
            }
        }
    }
}
