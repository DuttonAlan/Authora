// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

namespace Authora.Core.Application.OAuth.Token;

/// <summary>
///     The successful response of the /token endpoint (RFC 6749, Section 5.1).
/// </summary>
/// <param name="AccessToken">The new Access Token.</param>
/// <param name="TokenType">The type of the Access Token, always <c>Bearer</c>.</param>
/// <param name="ExpiresIn">The lifetime of the Access Token in seconds.</param>
/// <param name="RefreshToken">The new Refresh Token, or null if the Client does not get one.</param>
/// <param name="Scope">The Scopes of the Access Token, space-delimited.</param>
/// <param name="IdToken">
///     The OpenID Connect ID Token, or null. Only issued when an Authorization Code with the Scope <c>openid</c> is
///     exchanged, not on refresh.
/// </param>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("id_token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IdToken = null)
{
    /// <summary>
    ///     The <c>token_type</c> of all Access Tokens.
    /// </summary>
    public const string BearerTokenType = "Bearer";

    /// <summary>
    ///     Returns a description without the tokens, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the response.</returns>
    public override string ToString() => $"TokenResponse({TokenType}, {ExpiresIn}, {Scope})";
}
