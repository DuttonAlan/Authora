// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Converts between <see cref="GrantTypes"/> and the <c>grant_type</c> values used by OAuth and the API,
///     e.g. <c>authorization_code</c>.
/// </summary>
public static class GrantTypeNames
{
    /// <summary>
    ///     The <c>grant_type</c> value of <see cref="GrantTypes.AuthorizationCode"/>.
    /// </summary>
    public const string AuthorizationCode = "authorization_code";

    /// <summary>
    ///     The <c>grant_type</c> value of <see cref="GrantTypes.RefreshToken"/>.
    /// </summary>
    public const string RefreshToken = "refresh_token";

    private static readonly (GrantTypes GrantType, string Name)[] Names =
    [
        (GrantTypes.AuthorizationCode, AuthorizationCode),
        (GrantTypes.RefreshToken, RefreshToken)
    ];

    /// <summary>
    ///     Converts a single <c>grant_type</c> value to its <see cref="GrantTypes"/> flag. Names are case-sensitive.
    /// </summary>
    /// <param name="name">The <c>grant_type</c> value, e.g. <c>authorization_code</c>.</param>
    /// <param name="grantType">The matching flag, or <see cref="GrantTypes.None"/> if the name is unknown.</param>
    /// <returns>True if the name is a known Grant Type.</returns>
    public static bool TryParse(string? name, out GrantTypes grantType)
    {
        foreach (var entry in Names)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                grantType = entry.GrantType;
                return true;
            }
        }

        grantType = GrantTypes.None;
        return false;
    }

    /// <summary>
    ///     Combines several <c>grant_type</c> values into <see cref="GrantTypes"/> flags. Duplicates are ignored.
    /// </summary>
    /// <param name="names">The <c>grant_type</c> values.</param>
    /// <returns>The combined flags.</returns>
    /// <exception cref="ArgumentException">Thrown when a name is unknown.</exception>
    public static GrantTypes Parse(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var grantTypes = GrantTypes.None;

        foreach (var name in names)
        {
            if (!TryParse(name, out var grantType))
            {
                throw new ArgumentException(
                    $"Grant Type '{name}' is unknown. Allowed values: {string.Join(", ", Names.Select(entry => entry.Name))}.",
                    nameof(names));
            }

            grantTypes |= grantType;
        }

        return grantTypes;
    }

    /// <summary>
    ///     Converts <see cref="GrantTypes"/> flags into their <c>grant_type</c> values.
    /// </summary>
    /// <param name="grantTypes">The flags to convert.</param>
    /// <returns>The <c>grant_type</c> values of all set flags.</returns>
    public static IReadOnlyList<string> ToNames(GrantTypes grantTypes)
    {
        return Names
            .Where(entry => grantTypes.HasFlag(entry.GrantType))
            .Select(entry => entry.Name)
            .ToList();
    }
}
