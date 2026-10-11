// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Domain;

/// <summary>
///     Converts between the space-delimited <c>scope</c> parameter of OAuth (RFC 6749, Section 3.3) and a list of
///     Scope Names.
/// </summary>
/// <remarks>
///     Authorization Codes and tokens store their Scopes in this format, so the stored value can be returned in
///     responses as is. Scope Names are case-sensitive.
/// </remarks>
public static class ScopeList
{
    /// <summary>
    ///     Maximum length of a formatted <c>scope</c> value, which is how long Authorization Codes and tokens can
    ///     store it.
    /// </summary>
    public const int MaxLength = 4000;

    /// <summary>
    ///     Parses a <c>scope</c> parameter into distinct Scope Names, keeping their order.
    /// </summary>
    /// <param name="value">The <c>scope</c> parameter, e.g. <c>openid orders.read</c>.</param>
    /// <param name="scopes">The distinct Scope Names, or an empty list if the value is invalid or empty.</param>
    /// <returns>
    ///     True if the value contains at least one Scope, every Scope Name is valid and the formatted Scopes are at
    ///     most <see cref="MaxLength"/> characters long.
    /// </returns>
    public static bool TryParse(string? value, out IReadOnlyList<string> scopes)
    {
        scopes = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var names = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!names.All(Scope.IsValidName))
        {
            return false;
        }

        var distinct = names.Distinct(StringComparer.Ordinal).ToList();

        // Rejected here, so the request fails with invalid_scope instead of a database error when the code is saved.
        if (GetFormattedLength(distinct) > MaxLength)
        {
            return false;
        }

        scopes = distinct;

        return true;
    }

    /// <summary>
    ///     Parses a <c>scope</c> value that was stored by <see cref="Format(IEnumerable{string})"/>.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The Scope Names.</returns>
    public static IReadOnlyList<string> Split(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///     Formats Scope Names as a space-delimited <c>scope</c> value.
    /// </summary>
    /// <param name="scopes">The Scope Names.</param>
    /// <returns>The <c>scope</c> value.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when there is no Scope, a Scope Name is invalid or the value would be longer than <see cref="MaxLength"/>.
    /// </exception>
    public static string Format(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var names = scopes.Distinct(StringComparer.Ordinal).ToList();

        if (names.Count == 0 || !names.All(Scope.IsValidName))
        {
            throw new ArgumentException("At least one valid Scope Name is required.", nameof(scopes));
        }

        if (GetFormattedLength(names) > MaxLength)
        {
            throw new ArgumentException($"The Scopes must be at most {MaxLength} characters long.", nameof(scopes));
        }

        return string.Join(' ', names);
    }

    private static int GetFormattedLength(IReadOnlyCollection<string> names)
    {
        return names.Sum(name => name.Length) + names.Count - 1;
    }
}
