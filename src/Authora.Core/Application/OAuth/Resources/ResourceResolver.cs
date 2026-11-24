// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.OAuth.Resources;

/// <summary>
///     Resolves the <c>resource</c> parameter (RFC 8707) and decides for whom an Access Token is meant (its audience)
///     and which of its Scopes it keeps.
/// </summary>
/// <remarks>
///     <para>
///         A resource is an API, a Confidential Client with a <see cref="Client.ResourceIdentifier"/>, or Authora
///         itself (<c>{Issuer}/api</c>), which serves the System Scopes. A <c>resource</c> value must name an enabled
///         API or Authora, otherwise the request fails with <see cref="OAuthErrors.InvalidTarget"/>.
///     </para>
///     <para>
///         With resources, the token is meant for them and keeps only the Scopes they serve. Without, it is meant for
///         every Client that serves one of its Scopes (except APIs with
///         <see cref="Client.RequiresResourceIndicator"/>), and for Authora if it has a System Scope, and keeps all
///         Scopes, so setups from before resource indicators keep working.
///     </para>
/// </remarks>
/// <param name="clientRepository">An Instance of a IClientRepository to find the APIs.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
/// <param name="logger">The Logger to record rejected values.</param>
public sealed class ResourceResolver(
    IClientRepository clientRepository,
    IOptions<OAuthOptions> options,
    ILogger<ResourceResolver> logger)
{
    /// <summary>
    ///     The most <c>resource</c> values one request may contain.
    /// </summary>
    public const int MaxResources = 10;

    /// <summary>
    ///     The Resource Identifier of Authora itself: <c>{Issuer}/api</c>.
    /// </summary>
    public string AuthoraIdentifier => AuthoraResource.GetIdentifier(options.Value.Issuer);

    /// <summary>
    ///     Resolves the <c>resource</c> values of a request to the resources they name.
    /// </summary>
    /// <param name="client">The requesting Client, with its allowed Scopes.</param>
    /// <param name="values">The <c>resource</c> values, at least one.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The resources, without duplicates.</returns>
    /// <exception cref="OAuthException">
    ///     Thrown with <see cref="OAuthErrors.InvalidTarget"/> when a value is no absolute URI without fragment, or
    ///     names neither an enabled API nor Authora, or there are too many.
    /// </exception>
    public async Task<IReadOnlyList<Resource>> ResolveAsync(
        Client client,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(values);

        var distinct = values.Distinct(StringComparer.Ordinal).ToList();

        if (distinct.Count > MaxResources)
        {
            throw Reject(client, distinct[MaxResources], $"At most {MaxResources} resource parameters are allowed.");
        }

        var resources = new List<Resource>(distinct.Count);

        foreach (var value in distinct)
        {
            // RFC 8707, Section 2: an absolute URI without fragment.
            if (value.Length > Client.ResourceIdentifierMaxLength
                || !Uri.TryCreate(value, UriKind.Absolute, out var parsed)
                || !string.IsNullOrEmpty(parsed.Fragment)
                || value.Contains('#', StringComparison.Ordinal))
            {
                throw Reject(client, value, "The resource parameter must be an absolute URI without a fragment.");
            }

            if (string.Equals(value, AuthoraIdentifier, StringComparison.Ordinal))
            {
                resources.Add(GetAuthora(client));
                continue;
            }

            var api = await clientRepository.GetEnabledApiByResourceIdentifier(value, cancellationToken)
                ?? throw Reject(client, value, "The resource names no enabled API.");

            resources.Add(ToResource(api));
        }

        return resources;
    }

    /// <summary>
    ///     Checks the <c>resource</c> values of an authorization request against its Scopes: every requested Scope that
    ///     is not a System Scope must be served by one of the resources, and every resource must serve one of the
    ///     Scopes.
    /// </summary>
    /// <param name="client">The requesting Client, with its allowed Scopes.</param>
    /// <param name="scopes">The requested Scopes.</param>
    /// <param name="values">The <c>resource</c> values, at least one.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Ids of the resources, to store with the Authorization Code.</returns>
    /// <exception cref="OAuthException">Thrown with <see cref="OAuthErrors.InvalidTarget"/> when a check fails.</exception>
    public async Task<IReadOnlyList<Guid>> ValidateAuthorizationResourcesAsync(
        Client client,
        IReadOnlyList<Scope> scopes,
        IReadOnlyCollection<string> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var resources = await ResolveAsync(client, values, cancellationToken);
        var scopeNames = scopes.Select(scope => scope.Name).ToList();

        // System Scopes are approved for the ID Token and the Grant even without Authora as a resource; they are only
        // cut from the Access Token.
        var notServed = scopes.FirstOrDefault(scope => !scope.IsSystem && !resources.Any(resource => resource.ServedScopes.Contains(scope.Name)));

        if (notServed is not null)
        {
            throw Reject(client, string.Join(' ', values), $"The Scope {notServed.Name} is served by none of the requested resources.");
        }

        EnsureEachServesAny(client, resources, scopeNames);

        return resources.Select(resource => resource.Id).ToList();
    }

    /// <summary>
    ///     Decides the audience and the Scopes of a new Access Token.
    /// </summary>
    /// <param name="client">The Client the token is issued to, with its allowed Scopes.</param>
    /// <param name="scopes">The Scopes the token may have at most.</param>
    /// <param name="approved">
    ///     The resources of the Grant. If there are any, requested resources must be among them (downscoping), and
    ///     without a request the token is meant for all of them.
    /// </param>
    /// <param name="requested">The <c>resource</c> values of the token request, or empty.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Scopes and the audience of the token.</returns>
    /// <exception cref="OAuthException">
    ///     Thrown with <see cref="OAuthErrors.InvalidTarget"/> when a requested resource is invalid, not approved or
    ///     serves none of the Scopes, or none of the Scopes is served by the resources.
    /// </exception>
    public async Task<TokenAudience> SelectAsync(
        Client client,
        IReadOnlyList<string> scopes,
        IReadOnlyCollection<Guid> approved,
        IReadOnlyCollection<string> requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(approved);
        ArgumentNullException.ThrowIfNull(requested);

        if (requested.Count > 0)
        {
            var resources = await ResolveAsync(client, requested, cancellationToken);

            // RFC 8707, Section 2.2: the token request may only narrow the resources of the authorization request.
            var notApproved = approved.Count == 0 ? null : resources.FirstOrDefault(resource => !approved.Contains(resource.Id));

            if (notApproved is not null)
            {
                throw Reject(client, notApproved.Identifier, "A resource was not part of the authorization request.");
            }

            EnsureEachServesAny(client, resources, scopes);

            return Restrict(client, scopes, resources);
        }

        if (approved.Count > 0)
        {
            // An API may have stopped serving the Scopes, or been deleted, since the User approved; it is left out.
            var resources = (await GetByIdsAsync(client, approved, cancellationToken))
                .Where(resource => resource.ServesAny(scopes))
                .ToList();

            return Restrict(client, scopes, resources);
        }

        return new TokenAudience(scopes, await GetDefaultAudienceAsync(client, scopes, cancellationToken));
    }

    /// <summary>
    ///     Returns the Resource Identifiers of an audience, for <c>aud</c> at introspection: <c>{Issuer}/api</c> for
    ///     Authora, the Resource Identifier of an API, or the Client Id of a Client without one. Clients that no
    ///     longer exist are left out.
    /// </summary>
    /// <param name="audience">The audience of the token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The identifiers, in the order of the audience.</returns>
    public async Task<IReadOnlyList<string>> GetIdentifiersAsync(
        IReadOnlyCollection<Guid> audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        var clients = (await clientRepository.GetClientsWithServedScopes(
                audience.Where(id => id != AuthoraResource.Id).ToList(),
                cancellationToken))
            .ToDictionary(client => client.Id);

        var identifiers = new List<string>(audience.Count);

        foreach (var id in audience)
        {
            if (id == AuthoraResource.Id)
            {
                identifiers.Add(AuthoraIdentifier);
            }
            else if (clients.TryGetValue(id, out var client))
            {
                identifiers.Add(client.ResourceIdentifier ?? client.Id.ToString());
            }
        }

        return identifiers;
    }

    /// <summary>
    ///     The audience of a token requested without <c>resource</c>: every Client that serves one of its Scopes,
    ///     except APIs that require the <c>resource</c> parameter, and Authora if one of them is a System Scope.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetDefaultAudienceAsync(
        Client client,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var audience = new List<Guid>();

        if (GetAuthora(client).ServesAny(scopes))
        {
            audience.Add(AuthoraResource.Id);
        }

        audience.AddRange(await clientRepository.GetIdsOfClientsServingAnyScope(scopes.ToList(), cancellationToken));

        return audience;
    }

    private async Task<IReadOnlyList<Resource>> GetByIdsAsync(
        Client client,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var resources = new List<Resource>();

        if (ids.Contains(AuthoraResource.Id))
        {
            resources.Add(GetAuthora(client));
        }

        var apis = await clientRepository.GetClientsWithServedScopes(
            ids.Where(id => id != AuthoraResource.Id).ToList(),
            cancellationToken);

        resources.AddRange(apis.Select(ToResource));

        return resources;
    }

    /// <summary>
    ///     Cuts the Scopes to those the resources serve; the resources become the audience.
    /// </summary>
    private TokenAudience Restrict(Client client, IReadOnlyList<string> scopes, IReadOnlyList<Resource> resources)
    {
        var served = scopes.Where(scope => resources.Any(resource => resource.ServedScopes.Contains(scope))).ToList();

        if (served.Count == 0)
        {
            throw Reject(
                client,
                string.Join(' ', resources.Select(resource => resource.Identifier)),
                "None of the Scopes is served by the resources.");
        }

        return new TokenAudience(served, resources.Select(resource => resource.Id).ToList());
    }

    private void EnsureEachServesAny(Client client, IReadOnlyList<Resource> resources, IReadOnlyList<string> scopes)
    {
        var unused = resources.FirstOrDefault(resource => !resource.ServesAny(scopes));

        if (unused is not null)
        {
            throw Reject(client, unused.Identifier, "A resource serves none of the requested Scopes.");
        }
    }

    /// <summary>
    ///     Authora as a resource. It serves the System Scopes; those the Client is allowed are all it can request.
    /// </summary>
    private Resource GetAuthora(Client client)
    {
        var systemScopes = client.AllowedScopes
            .Where(scope => scope.IsSystem)
            .Select(scope => scope.Name)
            .ToHashSet(StringComparer.Ordinal);

        return new Resource(AuthoraResource.Id, AuthoraIdentifier, systemScopes);
    }

    private static Resource ToResource(Client api)
    {
        return new Resource(
            api.Id,
            api.ResourceIdentifier ?? api.Id.ToString(),
            api.ServedScopes.Where(scope => !scope.IsSystem).Select(scope => scope.Name).ToHashSet(StringComparer.Ordinal));
    }

    private OAuthException Reject(Client client, string resource, string description)
    {
        logger.ResourceRejected(client.Id, resource, description);

        return new OAuthException(OAuthErrors.InvalidTarget, description);
    }
}
