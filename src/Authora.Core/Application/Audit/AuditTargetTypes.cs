// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit;

/// <summary>
///     The kinds of objects an audit event can be about.
/// </summary>
public static class AuditTargetTypes
{
    /// <summary>A User, by its Id.</summary>
    public const string User = "user";

    /// <summary>A Role, by its Id.</summary>
    public const string Role = "role";

    /// <summary>A Group, by its Id.</summary>
    public const string Group = "group";

    /// <summary>A Scope, by its Id.</summary>
    public const string Scope = "scope";

    /// <summary>A Client, by its Id.</summary>
    public const string Client = "client";

    /// <summary>A Grant, by its Id.</summary>
    public const string Grant = "grant";

    /// <summary>A signing key, by its <c>kid</c>.</summary>
    public const string SigningKey = "signing-key";

    /// <summary>A link of the hash chain of the audit log, by its Sequence.</summary>
    public const string AuditChainLink = "audit-chain-link";
}
