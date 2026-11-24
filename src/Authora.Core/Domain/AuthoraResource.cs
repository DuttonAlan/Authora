// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain;

/// <summary>
///     Authora itself as a resource (RFC 8707): its admin API and the UserInfo endpoint, which serve the System
///     Scopes (<c>openid</c>, <c>profile</c>, <c>email</c>, <c>authora.admin</c>).
/// </summary>
/// <remarks>
///     Audiences and resources are stored as Client Ids. Authora is no Client, so it is stored as <see cref="Id"/>,
///     which no Client can have.
/// </remarks>
public static class AuthoraResource
{
    /// <summary>
    ///     The Id that stands for Authora in an audience or a list of resources (the nil UUID).
    /// </summary>
    public static readonly Guid Id = Guid.Empty;

    /// <summary>
    ///     The path that is appended to the Issuer to form the Resource Identifier of Authora.
    /// </summary>
    public const string IdentifierPath = "/api";

    /// <summary>
    ///     Returns the Resource Identifier of Authora: <c>{Issuer}/api</c>.
    /// </summary>
    /// <param name="issuer">The Issuer Identifier, without a trailing <c>/</c>.</param>
    /// <returns>The Resource Identifier.</returns>
    public static string GetIdentifier(string issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);

        return issuer + IdentifierPath;
    }
}
