// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Authora.Api.OpenApi;

/// <summary>
///     Adds the validation attributes of positional record parameters to the schema,
///     as Swashbuckle only reads them from properties.
/// </summary>
public sealed class RecordValidationSchemaFilter : ISchemaFilter
{
    /// <inheritdoc/>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema { Properties: not null } objectSchema)
        {
            return;
        }

        var parameters = context.Type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters());

        foreach (var parameter in parameters)
        {
            var property = objectSchema.Properties
                .FirstOrDefault(property => string.Equals(property.Key, parameter.Name, StringComparison.OrdinalIgnoreCase))
                .Value as OpenApiSchema;

            if (property is null)
            {
                continue;
            }

            if (parameter.GetCustomAttribute<StringLengthAttribute>() is { } stringLength)
            {
                property.MaxLength = stringLength.MaximumLength;

                if (stringLength.MinimumLength > 0)
                {
                    property.MinLength = stringLength.MinimumLength;
                }
            }

            if (parameter.GetCustomAttribute<RegularExpressionAttribute>() is { } regularExpression)
            {
                property.Pattern = regularExpression.Pattern;
            }

            if (parameter.GetCustomAttribute<EmailAddressAttribute>() is not null)
            {
                property.Format = "email";
            }
        }
    }
}
