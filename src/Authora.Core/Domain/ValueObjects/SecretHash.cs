// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.ValueObjects;

/// <summary>
///     The one-way hash of a Secret, e.g. a Client Secret. Immutable and compared by value.
/// </summary>
/// <remarks>
///     A separate type keeps a plain-text Secret from being stored or compared as a hash by mistake,
///     because both are strings. Create it with <c>ISecretHasher.Hash</c>, never from a plain-text Secret.
/// </remarks>
public sealed record SecretHash
{
    private SecretHash(string value) => Value = value;

    /// <summary>
    ///     The encoded hash.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     Wraps an already computed hash, e.g. the result of a hasher or a value loaded from the database.
    /// </summary>
    /// <param name="value">The encoded hash. Must never be a plain-text Secret.</param>
    /// <returns>The wrapped hash.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is empty.</exception>
    public static SecretHash FromHashedValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new SecretHash(value);
    }

    /// <summary>
    ///     Returns a placeholder, so the hash is not written to logs by accident.
    /// </summary>
    /// <returns>A placeholder instead of the hash.</returns>
    public override string ToString() => "SecretHash(***)";
}
