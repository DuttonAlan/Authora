// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Authora.Core.Domain.Mfa;

/// <summary>
///     The format of recovery codes: <see cref="Length"/> characters from an alphabet without the easily confused
///     <c>0</c>, <c>1</c>, <c>I</c> and <c>O</c>, shown in groups of four, e.g. <c>7KQM-2WXF-HN4P-9TRB</c>.
/// </summary>
/// <remarks>
///     With 32 characters, a code has 80 bits of entropy, so its unsalted SHA-256 hash cannot be reversed by trying all
///     codes. Entered codes are normalized first: case, spaces and dashes do not matter.
/// </remarks>
public static class RecoveryCodeFormat
{
    /// <summary>
    ///     Number of characters of a code, without the dashes.
    /// </summary>
    public const int Length = 16;

    /// <summary>
    ///     Number of codes in a set.
    /// </summary>
    public const int CountPerSet = 10;

    private const int GroupLength = 4;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    ///     Generates a code, as it is shown to the User.
    /// </summary>
    /// <returns>The code in groups of four, e.g. <c>7KQM-2WXF-HN4P-9TRB</c>.</returns>
    public static string Generate()
    {
        var builder = new StringBuilder(Length + (Length / GroupLength) - 1);

        for (var index = 0; index < Length; index++)
        {
            if (index > 0 && index % GroupLength == 0)
            {
                builder.Append('-');
            }

            builder.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Normalizes an entered code to the form that is hashed: uppercase, without spaces and dashes.
    /// </summary>
    /// <param name="input">The entered text.</param>
    /// <param name="code">The normalized code, if the text has the format of one.</param>
    /// <returns><see langword="true"/> if the text has the format of a code.</returns>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? code)
    {
        code = null;

        if (string.IsNullOrEmpty(input) || input.Length > Length * 2)
        {
            return false;
        }

        var builder = new StringBuilder(Length);

        foreach (var character in input)
        {
            if (character == '-' || char.IsWhiteSpace(character))
            {
                continue;
            }

            var upper = char.ToUpperInvariant(character);

            if (!Alphabet.Contains(upper, StringComparison.Ordinal))
            {
                return false;
            }

            builder.Append(upper);
        }

        if (builder.Length != Length)
        {
            return false;
        }

        code = builder.ToString();

        return true;
    }
}
