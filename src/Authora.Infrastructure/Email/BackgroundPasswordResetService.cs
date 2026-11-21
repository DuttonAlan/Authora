// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.RequestPasswordReset;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Handles the requests of the <see cref="BackgroundPasswordResetQueue"/> one after another with
///     <see cref="ProcessPasswordResetRequestService"/>. A failed request is logged and not retried.
/// </summary>
/// <param name="queue">The queue to read.</param>
/// <param name="scopeFactory">Creates a scope with its own DbContext per request.</param>
/// <param name="logger">The Logger to record failed requests.</param>
public sealed class BackgroundPasswordResetService(
    BackgroundPasswordResetQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundPasswordResetService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (queue.Reader.Count > 0)
            {
                logger.QueuedPasswordResetsDropped(queue.Reader.Count);
            }
        }
    }

    private async Task ProcessAsync(QueuedPasswordResetRequest request, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ProcessPasswordResetRequestService>();

            await service.ExecuteAsync(request, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed request, e.g. while the database is unavailable, must not stop the processing of the others.
            logger.QueuedPasswordResetFailed(exception);
        }
    }
}
