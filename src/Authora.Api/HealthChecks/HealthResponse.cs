// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Api.HealthChecks;

/// <summary>
///     Represents the response of a health endpoint.
/// </summary>
/// <param name="Status">Overall status: the worst status of all checks.</param>
/// <param name="DurationMs">Time it took to run all checks, in milliseconds.</param>
/// <param name="Checks">Result of each check.</param>
public sealed record HealthResponse(
    HealthStatus Status,
    double DurationMs,
    IReadOnlyList<HealthCheckResponse> Checks);
