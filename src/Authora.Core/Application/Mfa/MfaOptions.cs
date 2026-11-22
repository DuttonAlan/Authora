// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Settings of multi-factor authentication, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class MfaOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Mfa";

    /// <summary>
    ///     Maximum length of <see cref="IssuerName"/>.
    /// </summary>
    public const int IssuerNameMaxLength = 64;

    /// <summary>
    ///     The name authenticator apps show for the account, next to the Email of the User, e.g. the name of your
    ///     company. Defaults to "Authora". Changing it does not change the entries Users already added.
    /// </summary>
    public string IssuerName { get; set; } = "Authora";
}
