// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Client Application registered with Authora that can request tokens via OAuth 2.1.
/// </summary>
/// <remarks>
///     The Client is the aggregate root for its <see cref="ClientSecret"/>s and <see cref="RedirectUri"/>s.
///     Both can only be changed through the methods of this class.
/// </remarks>
public sealed class Client
{
    private readonly List<ClientSecret> _secrets = [];

    private readonly List<RedirectUri> _redirectUris = [];

    private readonly List<Scope> _allowedScopes = [];

    /// <summary>
    ///     Id of the Client. Also used as the public OAuth <c>client_id</c>.
    /// </summary>
    /// <remarks>
    ///     The <c>client_id</c> is not a secret (RFC 6749, Section 2.2), so exposing it is safe.
    /// </remarks>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Display Name of the Client, e.g. shown to the User on the consent screen.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Lifetime of issued Access Tokens in seconds. Defaults to 1 hour.
    /// </summary>
    public int AccessTokenTTL { get; set; } = 3600;

    /// <summary>
    ///     Lifetime of issued Refresh Tokens in seconds. Defaults to 24 hours.
    /// </summary>
    public int RefreshTokenTTL { get; set; } = 86400;

    /// <summary>
    ///     Type of the Client. Decides whether the Client must authenticate at the /token endpoint.
    ///     Cannot be changed after registration.
    /// </summary>
    public required ClientType Type { get; init; }

    /// <summary>
    ///     Grant Types the Client is allowed to use at the /token endpoint.
    ///     Combine multiple values with <c>|</c> and check them with <see cref="Enum.HasFlag(Enum)"/>.
    /// </summary>
    public GrantTypes AllowedGrantTypes { get; set; } = GrantTypes.AuthorizationCode | GrantTypes.RefreshToken;

    /// <summary>
    ///     Secrets of the Client, including expired and revoked ones. Only Confidential Clients have Secrets.
    /// </summary>
    public IReadOnlyCollection<ClientSecret> Secrets => _secrets;

    /// <summary>
    ///     Registered Redirect URIs. The <c>redirect_uri</c> of an authorization request must exactly match one of them.
    /// </summary>
    public IReadOnlyCollection<RedirectUri> RedirectUris => _redirectUris;

    /// <summary>
    ///     Scopes the Client is allowed to request. Requested Scopes that are not in this list are rejected.
    /// </summary>
    public IReadOnlyCollection<Scope> AllowedScopes => _allowedScopes;

    /// <summary>
    ///     Status of the Client. A disabled Client cannot obtain new tokens.
    /// </summary>
    public ClientStatus Status { get; private set; } = ClientStatus.Enabled;

    /// <summary>
    ///     Point in time (UTC) when the Client was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Client was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Enables the Client so it can obtain tokens again.
    /// </summary>
    public void Enable()
    {
        Status = ClientStatus.Enabled;
    }

    /// <summary>
    ///     Disables the Client so it can no longer obtain new tokens.
    /// </summary>
    public void Disable()
    {
        Status = ClientStatus.Disabled;
    }

    /// <summary>
    ///     Registers a new Redirect URI. Does nothing if the URI is already registered.
    /// </summary>
    /// <param name="uri">The Redirect URI to register.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is not a valid Redirect URI.</exception>
    public void AddRedirectUri(string uri)
    {
        var redirectUri = RedirectUri.Create(uri);

        if (!_redirectUris.Contains(redirectUri))
        {
            _redirectUris.Add(redirectUri);
        }
    }

    /// <summary>
    ///     Removes a registered Redirect URI. Does nothing if the URI is not registered.
    /// </summary>
    /// <param name="uri">The Redirect URI to remove.</param>
    public void RemoveRedirectUri(string uri)
    {
        _redirectUris.RemoveAll(x => x.Matches(uri));
    }

    /// <summary>
    ///     Checks whether the requested Redirect URI exactly matches one of the registered ones.
    /// </summary>
    /// <param name="requested">The <c>redirect_uri</c> from the authorization request.</param>
    /// <returns><see langword="true"/> if the Redirect URI is registered, otherwise <see langword="false"/>.</returns>
    public bool IsRedirectUriAllowed(string requested)
    {
        return _redirectUris.Any(x => x.Matches(requested));
    }

    /// <summary>
    ///     Adds a new Secret to the Client.
    /// </summary>
    /// <param name="secretHash">The hash of the Secret. The plain-text Secret must never be passed to the domain.</param>
    /// <param name="expiresAt">Point in time (UTC) when the Secret expires, or <see langword="null"/> if it never expires.</param>
    /// <returns>The created Secret.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Client is not a Confidential Client.</exception>
    public ClientSecret AddSecret(string secretHash, DateTime? expiresAt)
    {
        if (Type != ClientType.Confidential)
        {
            throw new InvalidOperationException("Only confidential clients can have secrets.");
        }

        var secret = new ClientSecret { SecretHash = secretHash, ExpiresAt = expiresAt };
        _secrets.Add(secret);

        return secret;
    }

    /// <summary>
    ///     Revokes a Secret of the Client. Does nothing if the Secret does not exist or is already revoked.
    /// </summary>
    /// <param name="secretId">The Id of the Secret to revoke.</param>
    /// <param name="now">The current point in time (UTC).</param>
    public void RevokeSecret(Guid secretId, DateTime now)
    {
        _secrets.FirstOrDefault(x => x.Id == secretId)?.Revoke(now);
    }

    /// <summary>
    ///     Allows the Client to request a Scope. Does nothing if the Scope is already allowed.
    /// </summary>
    /// <remarks>
    ///     <see cref="AllowedScopes"/> must be loaded, otherwise the duplicate check cannot work.
    /// </remarks>
    /// <param name="scope">The Scope to allow.</param>
    public void AllowScope(Scope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (!_allowedScopes.Any(x => x.Id == scope.Id))
        {
            _allowedScopes.Add(scope);
        }
    }

    /// <summary>
    ///     Disallows the Client to request a Scope. Does nothing if the Scope is not allowed.
    /// </summary>
    /// <param name="scopeId">The Id of the Scope to disallow.</param>
    public void DisallowScope(Guid scopeId)
    {
        _allowedScopes.RemoveAll(x => x.Id == scopeId);
    }

    /// <summary>
    ///     Checks whether the Client is allowed to request a Scope.
    /// </summary>
    /// <param name="scopeName">The Name of the Scope from the <c>scope</c> parameter.</param>
    /// <returns><see langword="true"/> if the Scope is allowed, otherwise <see langword="false"/>.</returns>
    public bool IsScopeAllowed(string scopeName)
    {
        return _allowedScopes.Any(x => string.Equals(x.Name, scopeName, StringComparison.Ordinal));
    }
}
