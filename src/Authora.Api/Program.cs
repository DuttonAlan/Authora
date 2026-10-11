// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

using Authora.Api.ExceptionHandling;
using Authora.Api.HealthChecks;
using Authora.Api.OpenApi;
using Authora.Core;
using Authora.Infrastructure;
using Authora.Infrastructure.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    // Include the XML comments of the Api and of Core, which contains the request and response types.
    foreach (var xmlFile in Directory.EnumerateFiles(AppContext.BaseDirectory, "Authora.*.xml"))
    {
        options.IncludeXmlComments(xmlFile);
    }

    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();
    options.SchemaFilter<RecordValidationSchemaFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthReportWriter.WriteAsync
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthReportWriter.WriteAsync
});

app.UseHttpsRedirection();

app.Run();
