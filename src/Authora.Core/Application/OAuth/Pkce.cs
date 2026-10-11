// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Proof Key for Code Exchange (PKCE, RFC 7636). Only the method <c>S256</c> is supported, because
///     <c>plain</c> does not protect against an intercepted authorization request.
/// </summary>
public static class Pkce
{
    /// <summary>
    ///     The only supported <c>code_challenge_method</c>.
    /// </summary>
    public const string MethodS256 = "S256";

    /// <summary>
    ///     Length of an S256 code challenge: the Base64Url encoded SHA-256 hash without padding.
    /// </summary>
    public const int CodeChallengeLength = 43;

    /// <summary>
    ///     Minimum length of a code verifier (RFC 7636, Section 4.1).
    /// </summary>
    public const int CodeVerifierMinLength = 43;

    /// <summary>
    ///     Maximum length of a code verifier (RFC 7636, Section 4.1).
    /// </summary>
    public const int CodeVerifierMaxLength = 128;

    /// <summary>
    ///     Checks whether a value has the format of an S256 code challenge.
    /// </summary>
    /// <param name="codeChallenge">The <c>code_challenge</c> parameter.</param>
    /// <returns>True if the value is 43 Base64Url characters.</returns>
    public static bool IsValidCodeChallenge(string? codeChallenge)
    {
        return codeChallenge is { Length: CodeChallengeLength }
            && Base64Url.IsValid(codeChallenge, out var decodedLength)
            && decodedLength == SHA256.HashSizeInBytes;
    }

    /// <summary>
    ///     Checks whether a value has the format of a code verifier: 43 to 128 unreserved characters
    ///     (<c>A-Z a-z 0-9 - . _ ~</c>).
    /// </summary>
    /// <param name="codeVerifier">The <c>code_verifier</c> parameter.</param>
    /// <returns>True if the format is valid.</returns>
    public static bool IsValidCodeVerifier(string? codeVerifier)
    {
        return codeVerifier is { Length: >= CodeVerifierMinLength and <= CodeVerifierMaxLength }
            && codeVerifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');
    }

    /// <summary>
    ///     Checks in constant time whether a code verifier matches an S256 code challenge.
    /// </summary>
    /// <param name="codeVerifier">The <c>code_verifier</c> from the token request.</param>
    /// <param name="codeChallenge">The <c>code_challenge</c> from the authorization request.</param>
    /// <returns>True if <c>BASE64URL(SHA256(code_verifier))</c> equals the code challenge.</returns>
    public static bool Verify(string codeVerifier, string codeChallenge)
    {
        ArgumentNullException.ThrowIfNull(codeVerifier);
        ArgumentNullException.ThrowIfNull(codeChallenge);

        if (!IsValidCodeVerifier(codeVerifier) || !IsValidCodeChallenge(codeChallenge))
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var expected = Base64Url.DecodeFromChars(codeChallenge);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
