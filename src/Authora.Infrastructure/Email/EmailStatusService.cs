// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Logs at startup whether email delivery is enabled, so a missing setting is noticed before the first email.
/// </summary>
/// <param name="options">The email settings.</param>
/// <param name="logger">The Logger.</param>
public sealed class EmailStatusService(IOptions<EmailOptions> options, ILogger<EmailStatusService> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.IsEnabled)
        {
            logger.EmailDisabled();

            return Task.CompletedTask;
        }

        logger.EmailEnabled(settings.Host!, settings.EffectivePort, settings.TlsMode);

        if (settings.TlsMode == SmtpTlsMode.None)
        {
            logger.EmailUnencrypted();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
