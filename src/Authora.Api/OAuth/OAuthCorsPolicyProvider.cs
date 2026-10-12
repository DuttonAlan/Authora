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
///     Provides the CORS policy <see cref="PolicyName"/>, which lets browser-based Clients (SPAs) call the token and
///     revocation endpoints from their own origin.
/// </summary>
/// <remarks>
///     <para>
///         Allowed are the origins of the Redirect URIs of all enabled Clients, only <c>POST</c>, only the
///         <c>Content-Type</c> header and no credentials (cookies), so the Session cookie is never sent along.
///         The origins are cached for <see cref="OAuthOptions.CorsOriginsCacheDuration"/>.
///     </para>
///     <para>
///         Every other policy name, including the default policy, has no policy, so CORS stays disabled everywhere
///         else. Never use this policy for <c>/oauth/authorize</c>, <c>/account/*</c> or <c>/api/*</c>.
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

    private CachedPolicy? _cached;

    /// <inheritdoc/>
    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (policyName != PolicyName)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var cached = _cached;

        if (cached is not null && cached.ExpiresAt > now)
        {
            return cached.Policy;
        }

        // Concurrent requests after the expiry may all load the origins once. That is cheaper than a lock.
        var clientRepository = context.RequestServices.GetRequiredService<IClientRepository>();
        var redirectUris = await clientRepository.GetRedirectUrisOfEnabledClients(context.RequestAborted);

        var policy = BuildPolicy(redirectUris);

        _cached = new CachedPolicy(policy, now + options.Value.CorsOriginsCacheDuration);

        return policy;
    }

    private static CorsPolicy BuildPolicy(IEnumerable<string> redirectUris)
    {
        // The origin is the scheme, host and port, e.g. "https://app.example.com" for
        // "https://app.example.com/callback". Uri leaves out default ports, like browsers do.
        var origins = redirectUris
            .Select(redirectUri => new Uri(redirectUri).GetLeftPart(UriPartial.Authority))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CorsPolicyBuilder()
            .WithOrigins(origins)
            .WithMethods(HttpMethods.Post)
            .WithHeaders(HeaderNames.ContentType)
            .DisallowCredentials()
            .Build();
    }

    private sealed record CachedPolicy(CorsPolicy Policy, DateTimeOffset ExpiresAt);
}
