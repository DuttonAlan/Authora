// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Defines the kind of an external identity provider, which fixes some of its settings.
/// </summary>
public enum IdentityProviderPreset
{
    /// <summary>
    ///     Any OpenID Connect provider; the Issuer is set by the administrator.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///     Google: the Issuer is always <c>https://accounts.google.com</c>.
    /// </summary>
    Google = 1,
}
