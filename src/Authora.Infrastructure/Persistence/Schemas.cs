// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     Names of the database schemas and shared column sizes.
/// </summary>
internal static class Schemas
{
    /// <summary>
    ///     Users, Clients, Roles, Permissions and Scopes.
    /// </summary>
    public const string Identity = "identity";

    /// <summary>
    ///     The OAuth runtime state: Sessions, Authorization Codes, Grants, Access Tokens and Refresh Tokens.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    ///     Maximum length of a stored hash of a code or token (SHA-256, Base64Url encoded: 43 characters).
    /// </summary>
    public const int HashMaxLength = 64;

    /// <summary>
    ///     Maximum length of a space-delimited list of Scopes.
    /// </summary>
    public const int ScopeMaxLength = ScopeList.MaxLength;
}
