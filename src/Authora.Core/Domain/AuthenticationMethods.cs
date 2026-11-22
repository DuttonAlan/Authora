// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain;

/// <summary>
///     The authentication methods a User completed, as Authentication Method Reference values (RFC 8176), and their
///     stored format: space-delimited, distinct, in the order they were completed.
/// </summary>
/// <remarks>
///     Sessions record the methods of their sign-in and pass them on to the Authorization Codes issued in them, so ID
///     Tokens can name them in <c>amr</c>.
/// </remarks>
public static class AuthenticationMethods
{
    /// <summary>
    ///     Password-based authentication (<c>pwd</c>).
    /// </summary>
    public const string Password = "pwd";

    /// <summary>
    ///     A one-time code (<c>otp</c>, "one-time password" in RFC 8176), e.g. the code of an authenticator app or a recovery code.
    /// </summary>
    public const string OneTimeCode = "otp";

    /// <summary>
    ///     A proof of possession of a hardware-secured key (<c>hwk</c>), e.g. a passkey on a security key.
    /// </summary>
    public const string HardwareKey = "hwk";

    /// <summary>
    ///     A proof of possession of a software-secured key (<c>swk</c>), e.g. a synced passkey.
    /// </summary>
    public const string SoftwareKey = "swk";

    /// <summary>
    ///     Multiple-factor authentication (<c>mfa</c>): added to <c>amr</c> in ID Tokens when the User completed at
    ///     least two methods. Never stored as a method of its own.
    /// </summary>
    public const string MultiFactor = "mfa";

    /// <summary>
    ///     Maximum length of a formatted list of methods, which is how long Sessions and Authorization Codes store it.
    /// </summary>
    public const int MaxLength = 200;

    /// <summary>
    ///     Maximum length of a single method, e.g. <c>pwd</c> or <c>otp</c>.
    /// </summary>
    public const int MaxMethodLength = 32;

    /// <summary>
    ///     Checks whether a value can be stored as a method: lowercase ASCII letters and digits only, as all values of
    ///     RFC 8176 are.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> if the value is a valid method.</returns>
    public static bool IsValid(string? method)
    {
        return !string.IsNullOrEmpty(method)
            && method.Length <= MaxMethodLength
            && method.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character));
    }

    /// <summary>
    ///     Formats methods for storage: distinct, in their order.
    /// </summary>
    /// <param name="methods">The methods.</param>
    /// <returns>The space-delimited methods.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when there is no method, a method is invalid or the value would be longer than <see cref="MaxLength"/>.
    /// </exception>
    public static string Format(IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var distinct = methods.Distinct(StringComparer.Ordinal).ToList();

        if (distinct.Count == 0)
        {
            throw new ArgumentException("At least one authentication method is required.", nameof(methods));
        }

        if (!distinct.All(IsValid))
        {
            throw new ArgumentException("An authentication method is invalid.", nameof(methods));
        }

        var value = string.Join(' ', distinct);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"The authentication methods must be at most {MaxLength} characters long.", nameof(methods));
        }

        return value;
    }

    /// <summary>
    ///     Checks whether the methods are a multi-factor authentication: at least two distinct methods, e.g. the
    ///     password and a one-time code.
    /// </summary>
    /// <param name="methods">The completed methods.</param>
    /// <returns><see langword="true"/> if at least two factors were completed.</returns>
    public static bool IsMultiFactor(IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        return methods
            .Where(method => !string.Equals(method, MultiFactor, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Count() >= 2;
    }

    /// <summary>
    ///     Checks whether the methods contain a passkey (<see cref="HardwareKey"/> or <see cref="SoftwareKey"/>), which
    ///     is phishing-resistant.
    /// </summary>
    /// <param name="methods">The completed methods.</param>
    /// <returns><see langword="true"/> if a passkey was used.</returns>
    public static bool ContainsPasskey(IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        return methods.Any(method => method is HardwareKey or SoftwareKey);
    }

    /// <summary>
    ///     The <c>amr</c> claim of an ID Token: the completed methods in their order, plus <see cref="MultiFactor"/>
    ///     if there were at least two (RFC 8176).
    /// </summary>
    /// <param name="methods">The completed methods.</param>
    /// <returns>The values of the claim.</returns>
    public static IReadOnlyList<string> ToAmrClaim(IReadOnlyCollection<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var values = methods.Distinct(StringComparer.Ordinal).ToList();

        if (IsMultiFactor(values) && !values.Contains(MultiFactor, StringComparer.Ordinal))
        {
            values.Add(MultiFactor);
        }

        return values;
    }

    /// <summary>
    ///     Parses methods that were stored by <see cref="Format(IEnumerable{string})"/>.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The methods.</returns>
    public static IReadOnlyList<string> Split(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
