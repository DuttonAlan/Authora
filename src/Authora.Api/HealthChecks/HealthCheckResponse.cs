// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Api.HealthChecks;

/// <summary>
///     Represents the result of a single health check.
/// </summary>
/// <param name="Name">Name of the check, e.g. <c>database</c>.</param>
/// <param name="Status">Status of the check.</param>
/// <param name="Description">Description of the result, or null if the check has none.</param>
/// <param name="DurationMs">Time it took to run the check, in milliseconds.</param>
/// <param name="Data">Additional data of the check, e.g. the pending migrations.</param>
public sealed record HealthCheckResponse(
    string Name,
    HealthStatus Status,
    string? Description,
    double DurationMs,
    IReadOnlyDictionary<string, object> Data);
