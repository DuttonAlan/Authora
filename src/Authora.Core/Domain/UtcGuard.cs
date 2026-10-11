// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace Authora.Core.Domain;

/// <summary>
///     Ensures that points in time passed to the domain are in UTC.
/// </summary>
/// <remarks>
///     The domain stores and compares UTC only. A local time, e.g. from <see cref="DateTime.Now"/>, would silently shift
///     every expiry check by the offset of the server, e.g. keep an expired Secret active for hours.
/// </remarks>
internal static class UtcGuard
{
    /// <summary>
    ///     Throws when a point in time is not in UTC.
    /// </summary>
    /// <param name="value">The point in time to check.</param>
    /// <param name="paramName">The name of the parameter. Filled in by the compiler.</param>
    /// <exception cref="ArgumentException">Thrown when <see cref="DateTime.Kind"/> is not <see cref="DateTimeKind.Utc"/>.</exception>
    public static void EnsureUtc(DateTime value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"The point in time must be in UTC, but its kind is {value.Kind}.", paramName);
        }
    }
}
