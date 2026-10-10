// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Defines the Client Types according to OAuth 2.1.
/// </summary>
public enum ClientType
{
    /// <summary>
    ///     The Client cannot keep a Secret confidential, e.g. a single-page, mobile or desktop app.
    ///     It does not authenticate at the /token endpoint and relies on PKCE.
    /// </summary>
    Public = 0,

    /// <summary>
    ///     The Client can keep a Secret confidential, e.g. a server-side web app.
    ///     It must authenticate at the /token endpoint and also uses PKCE.
    /// </summary>
    Confidential = 1
}
