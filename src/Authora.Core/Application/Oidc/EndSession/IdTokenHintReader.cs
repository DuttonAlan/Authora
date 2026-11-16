// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     Reads an <c>id_token_hint</c> and checks that Authora issued it.
/// </summary>
/// <remarks>
///     The signature must match one of the published keys, so a hint stays readable after a key rotation until the
///     previous key is removed, and <c>iss</c> must be Authora. The expiry is deliberately not checked: OpenID Connect
///     RP-Initiated Logout 1.0, Section 4, asks to accept expired ID Tokens, because Clients usually sign Users out
///     long after the ID Token expired.
/// </remarks>
/// <param name="signingKeyProvider">The published keys that may have signed the token.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
public sealed class IdTokenHintReader(ISigningKeyProvider signingKeyProvider, IOptions<OAuthOptions> options)
{
    /// <summary>
    ///     Longest accepted <c>id_token_hint</c>. Authora's ID Tokens are far shorter.
    /// </summary>
    public const int MaxLength = 16 * 1024;

    // 9999-12-31T23:59:59Z, the latest time DateTimeOffset.FromUnixTimeSeconds accepts.
    private const long MaxUnixTimeSeconds = 253402300799;

    /// <summary>
    ///     Reads an <c>id_token_hint</c>.
    /// </summary>
    /// <param name="token">The ID Token from the request.</param>
    /// <param name="hint">The claims logout needs, if the token is valid.</param>
    /// <returns>True if Authora issued the token, also if it is expired.</returns>
    public bool TryRead(string token, [NotNullWhen(true)] out IdTokenHint? hint)
    {
        ArgumentNullException.ThrowIfNull(token);

        hint = null;

        if (token.Length > MaxLength || !JsonWebToken.TryReadSigned(token, signingKeyProvider.PublishedKeys, out var claims))
        {
            return false;
        }

        if (GetString(claims, "iss") != options.Value.Issuer
            || !Guid.TryParseExact(GetString(claims, "sub"), "D", out var subject)
            || !Guid.TryParseExact(GetAudience(claims), "D", out var clientId)
            || !claims.TryGetProperty("auth_time", out var authTime)
            || authTime.ValueKind != JsonValueKind.Number
            || !authTime.TryGetInt64(out var authTimeSeconds)
            || authTimeSeconds is < 0 or > MaxUnixTimeSeconds)
        {
            return false;
        }

        hint = new IdTokenHint(subject, clientId, DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds).UtcDateTime);

        return true;
    }

    private static string? GetString(JsonElement claims, string name)
    {
        return claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    // Authora issues aud as a string. An array is accepted with exactly one value, as RFC 7519 allows both forms.
    private static string? GetAudience(JsonElement claims)
    {
        if (!claims.TryGetProperty("aud", out var audience))
        {
            return null;
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => audience.GetString(),
            JsonValueKind.Array when audience.GetArrayLength() == 1 && audience[0].ValueKind == JsonValueKind.String
                => audience[0].GetString(),
            _ => null
        };
    }
}
