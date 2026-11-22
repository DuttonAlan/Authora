// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain;

/// <summary>
///     The Authentication Context Class References (<c>acr</c>, OpenID Connect Core 1.0, Section 2) Authora assigns to
///     a sign-in, from the weakest to the strongest, and how they follow from the completed
///     <see cref="AuthenticationMethods"/>.
/// </summary>
/// <remarks>
///     Authora defines its own two levels, <see cref="Password"/> and <see cref="MultiFactor"/>, and uses
///     <see cref="PhishingResistant"/> of the OpenID Extended Authentication Profile (EAP) ACR Values 1.0 when a passkey
///     was used. A stronger level also satisfies every weaker one.
/// </remarks>
public static class AuthenticationContextClasses
{
    /// <summary>
    ///     Signed in with the password only.
    /// </summary>
    public const string Password = "urn:authora:acr:pwd";

    /// <summary>
    ///     Signed in with at least two factors, e.g. the password and a code of an authenticator app.
    /// </summary>
    public const string MultiFactor = "urn:authora:acr:mfa";

    /// <summary>
    ///     Phishing-resistant authentication (<c>phr</c>, OpenID EAP ACR Values 1.0): a passkey was used.
    /// </summary>
    public const string PhishingResistant = "phr";

    /// <summary>
    ///     All levels, from the weakest to the strongest.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Password, MultiFactor, PhishingResistant];

    /// <summary>
    ///     The level of a sign-in with the given methods.
    /// </summary>
    /// <param name="methods">The completed methods, e.g. <c>pwd</c> and <c>otp</c>.</param>
    /// <returns>The <c>acr</c> value.</returns>
    public static string Of(IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var list = methods as IReadOnlyCollection<string> ?? [.. methods];

        if (AuthenticationMethods.ContainsPasskey(list))
        {
            return PhishingResistant;
        }

        return AuthenticationMethods.IsMultiFactor(list) ? MultiFactor : Password;
    }

    /// <summary>
    ///     The strength of a level: 0 for the weakest in <see cref="All"/>, or -1 for a value Authora does not know.
    /// </summary>
    /// <param name="acr">The <c>acr</c> value.</param>
    /// <returns>The index of the level in <see cref="All"/>, or -1.</returns>
    public static int Rank(string acr)
    {
        ArgumentNullException.ThrowIfNull(acr);

        for (var index = 0; index < All.Count; index++)
        {
            if (string.Equals(All[index], acr, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Checks whether a sign-in with the given methods reaches at least <paramref name="acr"/>.
    /// </summary>
    /// <param name="methods">The completed methods.</param>
    /// <param name="acr">A level of <see cref="All"/>.</param>
    /// <returns><see langword="true"/> if the methods reach the level or a stronger one.</returns>
    public static bool Satisfies(IEnumerable<string> methods, string acr)
    {
        return Rank(Of(methods)) >= Rank(acr);
    }
}
