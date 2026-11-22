// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Authora.Core.Domain.Mfa;

/// <summary>
///     Time-based one-time passwords (TOTP, RFC 6238) with the parameters every authenticator app supports: HMAC-SHA-1,
///     6 digits and a time step of 30 seconds.
/// </summary>
/// <remarks>
///     SHA-1 is what RFC 6238 and the authenticator apps use by default; within HMAC its known collision weakness does
///     not apply. A code is accepted for the current time step and one step before and after, against clock drift.
/// </remarks>
public static class Totp
{
    /// <summary>
    ///     Number of digits of a code.
    /// </summary>
    public const int Digits = 6;

    /// <summary>
    ///     Length of a time step in seconds.
    /// </summary>
    public const int PeriodSeconds = 30;

    /// <summary>
    ///     Length of a generated secret in bytes: 160 bits, the length of the HMAC-SHA-1 key RFC 4226 recommends.
    /// </summary>
    public const int SecretLength = 20;

    /// <summary>
    ///     Number of time steps before and after the current one whose codes are accepted as well.
    /// </summary>
    public const int AllowedDrift = 1;

    private const int Modulus = 1_000_000;

    /// <summary>
    ///     Generates a random secret of <see cref="SecretLength"/> bytes.
    /// </summary>
    /// <returns>The secret.</returns>
    public static byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(SecretLength);

    /// <summary>
    ///     Gets the time step of a point in time: the number of <see cref="PeriodSeconds"/> since the Unix epoch.
    /// </summary>
    /// <param name="time">The point in time.</param>
    /// <returns>The time step.</returns>
    public static long GetTimeStep(DateTimeOffset time) => time.ToUnixTimeSeconds() / PeriodSeconds;

    /// <summary>
    ///     Checks whether a text has the format of a code: exactly <see cref="Digits"/> ASCII digits.
    /// </summary>
    /// <param name="code">The text.</param>
    /// <returns><see langword="true"/> if it can be a code.</returns>
    public static bool IsCodeFormat([NotNullWhen(true)] string? code)
    {
        return code is { Length: Digits } && code.All(char.IsAsciiDigit);
    }

    /// <summary>
    ///     Computes the code of a secret for a time step.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <param name="timeStep">The time step, see <see cref="GetTimeStep(DateTimeOffset)"/>.</param>
    /// <returns>The code, <see cref="Digits"/> digits with leading zeros.</returns>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 6238 and the authenticator apps use HMAC-SHA-1; HMAC does not depend on collision resistance.")]
    public static string ComputeCode(ReadOnlySpan<byte> secret, long timeStep)
    {
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);

        // Dynamic truncation, RFC 4226 section 5.3.
        var offset = hash[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.Slice(offset, sizeof(int))) & 0x7FFF_FFFF;

        return (binary % Modulus).ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Finds the time step a code belongs to, within <see cref="AllowedDrift"/> steps of the current one. The
    ///     comparison takes the same time whether or not and where a code matches.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <param name="code">The code the User entered.</param>
    /// <param name="now">The current point in time.</param>
    /// <returns>The matching time step, or <see langword="null"/> if the code matches none.</returns>
    public static long? FindTimeStep(ReadOnlySpan<byte> secret, string code, DateTimeOffset now)
    {
        if (!IsCodeFormat(code))
        {
            return null;
        }

        var current = GetTimeStep(now);
        var entered = Encoding.ASCII.GetBytes(code);
        long? match = null;

        for (var step = current - AllowedDrift; step <= current + AllowedDrift; step++)
        {
            var expected = Encoding.ASCII.GetBytes(ComputeCode(secret, step));

            // Every step is computed and compared, so the time does not tell which one matched.
            if (CryptographicOperations.FixedTimeEquals(expected, entered) && match is null)
            {
                match = step;
            }
        }

        return match;
    }

    /// <summary>
    ///     Builds the <c>otpauth://</c> URI that authenticator apps read from a QR code (Key Uri Format).
    /// </summary>
    /// <param name="issuer">The name of the service shown in the app, e.g. "Authora".</param>
    /// <param name="accountName">The account shown in the app, e.g. the Email of the User.</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The URI.</returns>
    public static string BuildUri(string issuer, string accountName, ReadOnlySpan<byte> secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var escapedIssuer = Uri.EscapeDataString(issuer);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{escapedIssuer}:{Uri.EscapeDataString(accountName)}?secret={Base32.Encode(secret)}&issuer={escapedIssuer}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}");
    }
}
