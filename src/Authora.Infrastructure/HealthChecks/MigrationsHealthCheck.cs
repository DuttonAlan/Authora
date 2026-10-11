// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Infrastructure.HealthChecks;

/// <summary>
///     Reports the migration status of the database and whether its schema matches this version of Authora (US-002).
/// </summary>
/// <remarks>
///     Pending migrations make the check fail, because the code expects a newer schema.
///     This includes an empty database and a migration that failed, as a failed migration is not recorded as applied.
///     Applied migrations that this version does not know, e.g. after a rollback of the application, are reported as degraded.
/// </remarks>
/// <param name="dbContext">The DbContext to read the migrations from.</param>
public sealed class MigrationsHealthCheck(AuthoraContext dbContext) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var known = dbContext.Database.GetMigrations().ToList();
        var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var pending = known.Except(applied).ToList();
        var unknown = applied.Except(known).ToList();

        var data = new Dictionary<string, object>
        {
            ["current"] = applied.LastOrDefault() ?? "none",
            ["latest"] = known.LastOrDefault() ?? "none",
            ["pending"] = pending,
            ["unknown"] = unknown
        };

        if (pending.Count > 0)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"The database schema is outdated: {pending.Count} migration(s) are pending.",
                data: data);
        }

        if (unknown.Count > 0)
        {
            return HealthCheckResult.Degraded(
                $"The database contains {unknown.Count} migration(s) unknown to this version.",
                data: data);
        }

        return HealthCheckResult.Healthy("The database schema is up to date.", data);
    }
}
