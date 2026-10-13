// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Authora.Api.OAuth;

/// <summary>
///     Provides the CORS policies that let browser-based Clients (SPAs) call Authora from their own origin.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="PolicyName"/> (token and revocation endpoints) allows the origins of the Redirect URIs of all
///         enabled Clients, only <c>POST</c> and only the <c>Content-Type</c> header. <see cref="UserInfoPolicyName"/>
///         allows the same origins, <c>GET</c> and <c>POST</c> and only the <c>Authorization</c> header, which carries
///         the Access Token. Neither allows credentials (cookies), so the Session cookie is never sent along. The
///         origins are cached for <see cref="OAuthOptions.CorsOriginsCacheDuration"/>.
///     </para>
///     <para>
///         <see cref="MetadataPolicyName"/> (discovery document and JWKS) allows every origin, but only <c>GET</c>
///         and no credentials, because the metadata is public.
///     </para>
///     <para>
///         Every other policy name, including the default policy, has no policy, so CORS stays disabled everywhere
///         else. Never use these policies for <c>/oauth/authorize</c>, <c>/account/*</c> or <c>/api/*</c>.
///     </para>
/// </remarks>
/// <param name="options">The OAuth settings with the cache duration.</param>
/// <param name="timeProvider">The clock for the cache expiry.</param>
public sealed class OAuthCorsPolicyProvider(IOptions<OAuthOptions> options, TimeProvider timeProvider) : ICorsPolicyProvider
{
    /// <summary>
    ///     The name of the policy, for <c>[EnableCors(OAuthCorsPolicyProvider.PolicyName)]</c>.
    /// </summary>
    public const string PolicyName = "oauth-clients";

    /// <summary>
    ///     The name of the policy of the UserInfo endpoint.
    /// </summary>
    public const string UserInfoPolicyName = "oidc-userinfo";

    /// <summary>
    ///     The name of the policy of the public discovery document and JWKS.
    /// </summary>
    public const string MetadataPolicyName = "oidc-metadata";

    private static readonly CorsPolicy MetadataPolicy = new CorsPolicyBuilder()
        .AllowAnyOrigin()
        .WithMethods(HttpMethods.Get)
        .DisallowCredentials()
        .Build();

    private CachedPolicies? _cached;

    /// <inheritdoc/>
    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        ArgumentNullException.ThrowIfNull(context);

        return policyName switch
        {
            MetadataPolicyName => MetadataPolicy,
            PolicyName => (await GetClientPoliciesAsync(context)).Token,
            UserInfoPolicyName => (await GetClientPoliciesAsync(context)).UserInfo,
            _ => null
        };
    }

    private async Task<CachedPolicies> GetClientPoliciesAsync(HttpContext context)
    {
        var now = timeProvider.GetUtcNow();
        var cached = _cached;

        if (cached is not null && cached.ExpiresAt > now)
        {
            return cached;
        }

        // Concurrent requests after the expiry may all load the origins once. That is cheaper than a lock.
        var clientRepository = context.RequestServices.GetRequiredService<IClientRepository>();
        var redirectUris = await clientRepository.GetRedirectUrisOfEnabledClients(context.RequestAborted);

        // The origin is the scheme, host and port, e.g. "https://app.example.com" for
        // "https://app.example.com/callback". Uri leaves out default ports, like browsers do.
        var origins = redirectUris
            .Select(redirectUri => new Uri(redirectUri).GetLeftPart(UriPartial.Authority))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var policies = new CachedPolicies(
            new CorsPolicyBuilder()
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Post)
                .WithHeaders(HeaderNames.ContentType)
                .DisallowCredentials()
                .Build(),
            new CorsPolicyBuilder()
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post)
                .WithHeaders(HeaderNames.Authorization)
                .DisallowCredentials()
                .Build(),
            now + options.Value.CorsOriginsCacheDuration);

        _cached = policies;

        return policies;
    }

    private sealed record CachedPolicies(CorsPolicy Token, CorsPolicy UserInfo, DateTimeOffset ExpiresAt);
}
