// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Infrastructure.HealthChecks;

/// <summary>
///     Tags to group health checks by the endpoint that runs them.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    ///     Checks that must pass before the instance can serve requests, e.g. database connectivity and migrations.
    /// </summary>
    public const string Ready = "ready";
}
