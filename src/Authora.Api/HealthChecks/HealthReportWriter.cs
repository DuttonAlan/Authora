// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Api.HealthChecks;

/// <summary>
///     Writes a <see cref="HealthReport"/> as JSON, including the status, description and data of each check.
/// </summary>
internal static class HealthReportWriter
{
    private const string FailedDescription = "The check failed with an unexpected error. See the logs for details.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///     Writes the report to the response.
    /// </summary>
    /// <remarks>
    ///     Exception messages are not written, as they can contain details about the infrastructure.
    ///     The health check service logs them instead.
    /// </remarks>
    /// <param name="httpContext">The HttpContext of the request.</param>
    /// <param name="report">The report to write.</param>
    /// <returns>A Task that completes once the report is written.</returns>
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        var checks = report.Entries
            .Select(entry => new HealthCheckResponse(
                entry.Key,
                entry.Value.Status,
                entry.Value.Exception is null ? entry.Value.Description : FailedDescription,
                entry.Value.Duration.TotalMilliseconds,
                entry.Value.Data))
            .ToList();

        var response = new HealthResponse(report.Status, report.TotalDuration.TotalMilliseconds, checks);

        httpContext.Response.ContentType = "application/json";

        return JsonSerializer.SerializeAsync(httpContext.Response.Body, response, SerializerOptions, httpContext.RequestAborted);
    }
}
