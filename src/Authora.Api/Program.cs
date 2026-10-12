// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.ExceptionHandling;
using Authora.Api.HealthChecks;
using Authora.Api.OAuth;
using Authora.Api.OpenApi;
using Authora.Api.Pages;
using Authora.Api.RateLimiting;
using Authora.Core;
using Authora.Core.Application.Account;
using Authora.Core.Application.OAuth;
using Authora.Infrastructure;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.HealthChecks;
using Authora.Infrastructure.Seeding;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;

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

    options.AddSecurityDefinition(AuthorizeOperationFilter.SecuritySchemeName, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "An Access Token of Authora with the Scope `authora.admin`, from `POST /oauth/token`."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRazorPages(options => options.Conventions.ConfigureFilter(new SecurityHeadersPageFilter()));

builder.Services.AddOptions<OAuthOptions>()
    .Bind(builder.Configuration.GetSection(OAuthOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<SignInOptions>()
    .Bind(builder.Configuration.GetSection(SignInOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<BootstrapOptions>()
    .Bind(builder.Configuration.GetSection(BootstrapOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<CleanupOptions>()
    .Bind(builder.Configuration.GetSection(CleanupOptions.SectionName))
    .ValidateOnStart();

// The Session cookie is only used by the sign-in and authorization pages. The admin API only accepts Access Tokens,
// see AuthorizationPolicies.
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, configureOptions: null)
    .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(AccessTokenAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization(options => options.AddAuthoraPolicies());
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddRateLimiter(options => options.AddAuthoraPolicies());

// CORS only for the token and revocation endpoints, see OAuthCorsPolicyProvider. Replaces the default provider,
// which AddControllers already registered.
builder.Services.AddCors();
builder.Services.Replace(ServiceDescriptor.Singleton<ICorsPolicyProvider, OAuthCorsPolicyProvider>());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Seeds the Permissions, the administrator Role and the first administrator before any request is served.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
}

app.UseExceptionHandler();

app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapRazorPages();

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

await app.RunAsync();
