// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Authora.Api.ExceptionHandling;

/// <summary>
///     Maps expected exceptions to problem details responses with a matching status code.
///     All other exceptions are left to the default handler, which returns 500.
/// </summary>
/// <param name="problemDetailsService">The Service to write the problem details response.</param>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException or InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),

            // A unique index was violated, e.g. by a concurrent request that passed the same uniqueness check.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "The resource conflicts with an existing resource."),
            
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (0, null)
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Detail = detail
            }
        });
    }
}
