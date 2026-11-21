// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Email;

/// <summary>
///     Settings of email delivery through an SMTP server, bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     Email delivery is disabled while <see cref="Host"/> is not set, and so are the features that send email.
/// </remarks>
public sealed class EmailOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Email";

    /// <summary>
    ///     Upper limit of <see cref="Timeout"/>.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Maximum length of <see cref="SenderName"/>.
    /// </summary>
    public const int SenderNameMaxLength = 100;

    /// <summary>
    ///     Host name or IP address of the SMTP server. Not set by default, which disables email delivery.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    ///     Port of the SMTP server. Defaults to 587 for <see cref="SmtpTlsMode.StartTls"/>, 465 for
    ///     <see cref="SmtpTlsMode.Implicit"/> and 25 for <see cref="SmtpTlsMode.None"/>.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    ///     How the connection is encrypted. Defaults to <see cref="SmtpTlsMode.StartTls"/>.
    ///     <see cref="SmtpTlsMode.None"/> is refused outside Development.
    /// </summary>
    public SmtpTlsMode TlsMode { get; set; } = SmtpTlsMode.StartTls;

    /// <summary>
    ///     User name for SMTP authentication, or <see langword="null"/> to send without authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Password for SMTP authentication. Required together with <see cref="Username"/>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     The address emails are sent from, e.g. <c>no-reply@example.com</c>. Required when <see cref="Host"/> is set.
    /// </summary>
    public string? SenderAddress { get; set; }

    /// <summary>
    ///     The display name emails are sent from. Defaults to <c>Authora</c>.
    /// </summary>
    public string SenderName { get; set; } = "Authora";

    /// <summary>
    ///     How long connecting to and talking to the SMTP server may take per email. Defaults to 30 seconds, at most
    ///     <see cref="MaxTimeout"/>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     PEM file with a certificate that is trusted as the root for the certificate of the SMTP server, e.g. a
    ///     private CA or a self-signed certificate. Not set by default: then the system's trusted CAs apply.
    /// </summary>
    /// <remarks>
    ///     The host name is still checked. Relative paths are resolved against the content root.
    /// </remarks>
    public string? TrustedCertificatePath { get; set; }

    /// <summary>
    ///     Whether email delivery is configured, i.e. <see cref="Host"/> is set.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Host);

    /// <summary>
    ///     <see cref="Port"/>, or the default port of the <see cref="TlsMode"/>.
    /// </summary>
    public int EffectivePort => Port ?? TlsMode switch
    {
        SmtpTlsMode.Implicit => 465,
        SmtpTlsMode.None => 25,
        _ => 587
    };
}
