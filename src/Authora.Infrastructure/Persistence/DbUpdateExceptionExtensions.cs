// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     Helpers to tell apart the causes of a failed save.
/// </summary>
internal static class DbUpdateExceptionExtensions
{
    /// <summary>
    ///     The detail the API also returns for a unique violation that reaches it unhandled.
    /// </summary>
    public const string UniqueViolationMessage = "The resource conflicts with an existing resource.";

    /// <summary>
    ///     Checks whether the save failed because a unique index or primary key was violated, e.g. by a concurrent
    ///     request that inserted the same row first.
    /// </summary>
    /// <param name="exception">The exception of the failed save.</param>
    /// <returns>True if a unique index or primary key was violated.</returns>
    public static bool IsUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
