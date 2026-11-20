// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Loads <see cref="EmailOptions.TrustedCertificatePath"/> at startup, so a missing or invalid file is reported
///     right away instead of with the first email.
/// </summary>
/// <param name="environment">The host environment, whose content root relative paths are resolved against.</param>
public sealed class EmailCertificateValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsEnabled || options.TrustedCertificatePath is null)
        {
            return ValidateOptionsResult.Skip;
        }

        try
        {
            using var _ = SmtpCertificateTrust.Load(options.TrustedCertificatePath, environment.ContentRootPath);
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(
                $"'{EmailOptions.SectionName}:{nameof(EmailOptions.TrustedCertificatePath)}': {exception.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
