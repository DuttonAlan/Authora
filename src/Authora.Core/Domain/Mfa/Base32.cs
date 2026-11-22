// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text;

namespace Authora.Core.Domain.Mfa;

/// <summary>
///     Base32 encoding (RFC 4648, section 6) without padding, the format authenticator apps expect for TOTP secrets.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    ///     Encodes bytes as uppercase Base32 without padding.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var value in data)
        {
            buffer = ((buffer << 8) | value) & 0xFFFF;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                builder.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Decodes Base32 text without padding, ignoring case.
    /// </summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="FormatException">Thrown when the text contains a character outside the alphabet.</exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new byte[text.Length * 5 / 8];
        var buffer = 0;
        var bits = 0;
        var index = 0;

        foreach (var character in text)
        {
            var value = Alphabet.IndexOf(char.ToUpperInvariant(character), StringComparison.Ordinal);

            if (value < 0)
            {
                throw new FormatException("The text is not Base32.");
            }

            buffer = ((buffer << 5) | value) & 0xFFFF;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                result[index++] = (byte)(buffer >> bits);
            }
        }

        return result;
    }
}
