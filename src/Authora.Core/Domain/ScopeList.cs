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
    ///     Parses a <c>scope</c> parameter into distinct Scope Names, keeping their order.
    /// </summary>
    /// <param name="value">The <c>scope</c> parameter, e.g. <c>openid orders.read</c>.</param>
    /// <param name="scopes">The distinct Scope Names, or an empty list if the value is invalid or empty.</param>
    /// <returns>True if the value contains at least one Scope and every Scope Name is valid.</returns>
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

        scopes = names.Distinct(StringComparer.Ordinal).ToList();

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
    /// <exception cref="ArgumentException">Thrown when there is no Scope or a Scope Name is invalid.</exception>
    public static string Format(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var names = scopes.Distinct(StringComparer.Ordinal).ToList();

        if (names.Count == 0 || !names.All(Scope.IsValidName))
        {
            throw new ArgumentException("At least one valid Scope Name is required.", nameof(scopes));
        }

        return string.Join(' ', names);
    }
}
