// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

using Authora.Api.AdminUi;
using Authora.Api.Auditing;
using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.ExceptionHandling;
using Authora.Api.HealthChecks;
using Authora.Api.OAuth;
using Authora.Api.OpenApi;
using Authora.Api.Pages;
using Authora.Api.RateLimiting;
using Authora.Api.Security;
using Authora.Core;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.AdminUi;
using Authora.Core.Application.Audit.Chain;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.Oidc;
using Authora.Infrastructure;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.HealthChecks;
using Authora.Infrastructure.Seeding;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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

// The antiforgery cookie of the sign-in and consent forms gets the same protection as the Session cookie: the
// __Host- prefix keeps subdomains from setting it, and Strict keeps it out of cross-site requests.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-authora_antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
});

// Encrypts the antiforgery tokens and the cookies of the admin interface (TD-014). Several instances must share the
// keys, and keys that are lost on a restart sign every administrator out, so production sets a shared directory.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Authora");
var dataProtectionKeysPath = builder.Configuration["Authora:DataProtection:KeysPath"];

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddSingleton<AdminUiCookies>();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
});

var forwardedHeaders = builder.Configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
    ?? new ForwardedHeadersSettings();
var startupFailures = forwardedHeaders.Validate().ToList();

if (builder.Environment.IsProduction())
{
    startupFailures.AddRange(ProductionSettingsCheck.Validate(builder.Configuration));
}

if (startupFailures.Count > 0)
{
    throw new InvalidOperationException(
        $"Authora is not configured securely:{Environment.NewLine}{string.Join(Environment.NewLine, startupFailures)}");
}

builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeaders.Apply);

builder.Services.AddOptions<OAuthOptions>()
    .Bind(builder.Configuration.GetSection(OAuthOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<OidcOptions>()
    .Bind(builder.Configuration.GetSection(OidcOptions.SectionName))
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
builder.Services.AddOptions<AuditChainOptions>()
    .Bind(builder.Configuration.GetSection(AuditChainOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<AdminUiOptions>()
    .Bind(builder.Configuration.GetSection(AdminUiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateOnStart();
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RateLimitOptions>, RateLimitOptionsValidator>());

// The actor of audit events is taken from the current request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuditActorAccessor, HttpContextAuditActorAccessor>();

// The Session cookie is only used by the sign-in and authorization pages. The admin API only accepts Access Tokens,
// see AuthorizationPolicies.
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, configureOptions: null)
    .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(AccessTokenAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization(options => options.AddAuthoraPolicies());
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddRateLimiter(options => options.AddAuthoraPolicies());

// CORS only for the endpoints Clients call from a browser, see OAuthCorsPolicyProvider. Replaces the default provider,
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

// First, so the IP address and scheme of the client are known to everything after it, e.g. the rate limits and the
// audit log.
if (forwardedHeaders.IsEnabled)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

var adminUiEnabled = app.Services.GetRequiredService<IOptions<AdminUiOptions>>().Value.Enabled;

if (adminUiEnabled)
{
    // Serves /admin/index.html for "/admin/", and redirects "/admin" to "/admin/".
    app.UseDefaultFiles();
}
else
{
    // The files stay in wwwroot, so they must be hidden explicitly.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments(AdminUiOptions.Path, StringComparison.OrdinalIgnoreCase),
        branch => branch.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }));
}

app.UseStaticFiles();
app.UseCors();

if (adminUiEnabled)
{
    // Before the authentication, so the admin API sees the Access Token of the admin interface's cookie as a Bearer
    // token (TD-014).
    app.UseMiddleware<AdminUiTokenMiddleware>();
}

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

// Public, for load balancers and orchestrators: only the overall status (200 or 503).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthReportWriter.WriteStatusAsync
});

// The details reveal the schema version, so they need an admin Access Token with the Permission.
app.MapHealthChecks("/health/details", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthReportWriter.WriteAsync
}).RequireAuthorization(AuthorizationPolicies.AdminApi, PermissionNames.Health.Read);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthReportWriter.WriteStatusAsync
});

await app.RunAsync();
