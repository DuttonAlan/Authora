// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

namespace Authora.Core.Application.OAuth.Introspect;

/// <summary>
///     The response of the introspection endpoint (RFC 7662, Section 2.2). An inactive token only returns
///     <c>{"active": false}</c>, without telling why.
/// </summary>
/// <param name="Active">Whether the token is active.</param>
/// <param name="Scope">The Scopes of the token, space-delimited.</param>
/// <param name="ClientId">The <c>client_id</c> the token was issued to.</param>
/// <param name="Subject">The Id of the User (<c>sub</c>).</param>
/// <param name="TokenType">The type of the token: <c>Bearer</c> for Access Tokens, null for Refresh Tokens.</param>
/// <param name="ExpiresAt">When the token expires, in seconds since the Unix epoch (<c>exp</c>).</param>
/// <param name="IssuedAt">When the token was issued, in seconds since the Unix epoch (<c>iat</c>).</param>
/// <param name="Issuer">The Issuer Identifier of Authora (<c>iss</c>).</param>
public sealed record IntrospectionResponse(
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("scope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Scope = null,
    [property: JsonPropertyName("client_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientId = null,
    [property: JsonPropertyName("sub"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subject = null,
    [property: JsonPropertyName("token_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenType = null,
    [property: JsonPropertyName("exp"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ExpiresAt = null,
    [property: JsonPropertyName("iat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? IssuedAt = null,
    [property: JsonPropertyName("iss"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Issuer = null)
{
    /// <summary>
    ///     The response for a token that is unknown, expired, revoked or may not be introspected by the caller.
    /// </summary>
    public static readonly IntrospectionResponse Inactive = new(false);
}
