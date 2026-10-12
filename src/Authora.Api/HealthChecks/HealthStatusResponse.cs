// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Api.HealthChecks;

/// <summary>
///     Represents the response of a public health endpoint, without the details of the checks.
/// </summary>
/// <param name="Status">Overall status: the worst status of all checks.</param>
public sealed record HealthStatusResponse(HealthStatus Status);
