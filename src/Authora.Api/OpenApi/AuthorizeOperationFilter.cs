// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Authora.Api.OpenApi;

/// <summary>
///     Adds the Bearer security requirement and the 401 and 403 responses to every operation that requires
///     authorization, so they do not have to be repeated on every action.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    /// <summary>
    ///     Name of the security scheme in the OpenAPI document.
    /// </summary>
    public const string SecuritySchemeName = "Bearer";

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var permissions = metadata.OfType<IAuthorizeData>()
            .Select(authorize => authorize.Policy)
            .Where(policy => policy is not null && PermissionNames.All.Contains(policy))
            .ToList();

        if (permissions.Count > 0)
        {
            operation.Description = $"{operation.Description}\n\nRequires the Permission `{string.Join("`, `", permissions)}`.".Trim();
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse { Description = "The Access Token is missing or not active." });
        operation.Responses.TryAdd(
            StatusCodes.Status403Forbidden.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse { Description = $"The Access Token lacks the Scope `{AdminApiScope.Name}`, or the User lacks the Permission." });

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeName, context.Document)] = []
        });
    }
}
