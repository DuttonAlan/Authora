// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Email;

/// <summary>
///     How the connection to the SMTP server is encrypted.
/// </summary>
public enum SmtpTlsMode
{
    /// <summary>
    ///     Connects unencrypted and upgrades with STARTTLS, which the server must support (submission port 587).
    /// </summary>
    StartTls = 0,

    /// <summary>
    ///     Connects with TLS right away (implicit TLS, port 465).
    /// </summary>
    Implicit = 1,

    /// <summary>
    ///     Unencrypted. Credentials and links travel in plain text, so only allowed in Development and Testing.
    /// </summary>
    None = 2
}
