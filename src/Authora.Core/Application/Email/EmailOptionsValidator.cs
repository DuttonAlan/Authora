// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net.Mail;

using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Email;

/// <summary>
///     Validates the <see cref="EmailOptions"/> at startup.
/// </summary>
/// <remarks>
///     Without <see cref="EmailOptions.Host"/>, email delivery is disabled and nothing else is checked. Whether
///     <see cref="SmtpTlsMode.None"/> is allowed depends on the environment and is checked by the host.
/// </remarks>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        var section = EmailOptions.SectionName;

        if (Uri.CheckHostName(options.Host) == UriHostNameType.Unknown)
        {
            failures.Add($"'{section}:{nameof(EmailOptions.Host)}' must be a host name or an IP address, without scheme or port.");
        }

        if (options.Port is < 1 or > 65535)
        {
            failures.Add($"'{section}:{nameof(EmailOptions.Port)}' must be between 1 and 65535.");
        }

        if (!Enum.IsDefined(options.TlsMode))
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.TlsMode)}' must be one of {string.Join(", ", Enum.GetNames<SmtpTlsMode>())}.");
        }

        if (string.IsNullOrWhiteSpace(options.SenderAddress))
        {
            failures.Add($"'{section}:{nameof(EmailOptions.SenderAddress)}' is required when '{section}:{nameof(EmailOptions.Host)}' is set.");
        }
        else if (!IsPlainAddress(options.SenderAddress))
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.SenderAddress)}' must be a single email address without display name, "
                + $"e.g. no-reply@example.com. Set the name in '{nameof(EmailOptions.SenderName)}'.");
        }

        if (string.IsNullOrWhiteSpace(options.SenderName)
            || options.SenderName.Length > EmailOptions.SenderNameMaxLength
            || options.SenderName.Any(char.IsControl))
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.SenderName)}' must be 1 to {EmailOptions.SenderNameMaxLength} characters "
                + "without line breaks or control characters.");
        }

        if (string.IsNullOrEmpty(options.Username) != string.IsNullOrEmpty(options.Password))
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.Username)}' and '{section}:{nameof(EmailOptions.Password)}' must be set together.");
        }

        if (options.Timeout < TimeSpan.FromSeconds(1) || options.Timeout > EmailOptions.MaxTimeout)
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.Timeout)}' must be between 00:00:01 and {EmailOptions.MaxTimeout}. "
                + "Use the format hh:mm:ss, e.g. 00:00:30; a plain number is read as days.");
        }

        if (options.TrustedCertificatePath is not null && options.TlsMode == SmtpTlsMode.None)
        {
            failures.Add(
                $"'{section}:{nameof(EmailOptions.TrustedCertificatePath)}' requires '{nameof(EmailOptions.TlsMode)}' "
                + $"{nameof(SmtpTlsMode.StartTls)} or {nameof(SmtpTlsMode.Implicit)}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsPlainAddress(string value)
    {
        return value.Length <= User.EmailMaxLength
            && MailAddress.TryCreate(value, out var address)
            && string.Equals(address.Address, value, StringComparison.Ordinal);
    }
}
