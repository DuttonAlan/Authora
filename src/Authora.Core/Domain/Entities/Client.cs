// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Client Application registered with Authora that can request tokens via OAuth 2.1.
/// </summary>
/// <remarks>
///     The Client is the aggregate root for its <see cref="ClientSecret"/>s and <see cref="RedirectUri"/>s.
///     Both can only be changed through the methods of this class.
///     A Client can only be created via <see cref="Create(string, ClientType)"/>, so an invalid Client can never exist.
/// </remarks>
public sealed class Client
{
    /// <summary>
    ///     Maximum length of the Name.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    ///     Default lifetime of Access Tokens in seconds (1 hour).
    /// </summary>
    public const int DefaultAccessTokenTTL = 3600;

    /// <summary>
    ///     Default lifetime of Refresh Tokens in seconds (24 hours).
    /// </summary>
    public const int DefaultRefreshTokenTTL = 86400;

    /// <summary>
    ///     Upper limit of the Access Token lifetime in seconds (24 hours). Resource servers may check Access Tokens
    ///     without calling Authora, so a long lifetime keeps revoked access usable.
    /// </summary>
    public const int MaxAccessTokenTTL = 86400;

    /// <summary>
    ///     Upper limit of the Refresh Token lifetime in seconds (90 days).
    /// </summary>
    public const int MaxRefreshTokenTTL = 7776000;

    /// <summary>
    ///     Default Grant Types of a new Client.
    /// </summary>
    public const GrantTypes DefaultGrantTypes = GrantTypes.AuthorizationCode | GrantTypes.RefreshToken;

    private const GrantTypes AllGrantTypes = GrantTypes.AuthorizationCode | GrantTypes.RefreshToken;

    private readonly List<ClientSecret> _secrets = [];
    private readonly List<RedirectUri> _redirectUris = [];
    private readonly List<Scope> _allowedScopes = [];

    /// <summary>
    ///     Used by EF Core to materialize a Client from the database.
    /// </summary>
    private Client()
    {
    }

    /// <summary>
    ///     Id of the Client. Also used as the public OAuth <c>client_id</c>.
    /// </summary>
    /// <remarks>
    ///     The <c>client_id</c> is not a secret (RFC 6749, Section 2.2), so exposing it is safe.
    /// </remarks>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Display Name of the Client, e.g. shown to the User on the consent screen. Set via <see cref="SetName(string)"/>.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     Lifetime of issued Access Tokens in seconds. Defaults to 1 hour.
    ///     Set via <see cref="SetTokenLifetimes(int, int)"/>.
    /// </summary>
    public int AccessTokenTTL { get; private set; } = DefaultAccessTokenTTL;

    /// <summary>
    ///     Lifetime of issued Refresh Tokens in seconds. Defaults to 24 hours. Always longer than <see cref="AccessTokenTTL"/>.
    ///     Set via <see cref="SetTokenLifetimes(int, int)"/>.
    /// </summary>
    public int RefreshTokenTTL { get; private set; } = DefaultRefreshTokenTTL;

    /// <summary>
    ///     Type of the Client. Decides whether the Client must authenticate at the /token endpoint.
    ///     Cannot be changed after registration.
    /// </summary>
    public ClientType Type { get; private set; }

    /// <summary>
    ///     Grant Types the Client is allowed to use at the /token endpoint.
    ///     Set via <see cref="SetAllowedGrantTypes(GrantTypes)"/>.
    ///     Check a requested Grant Type with <see cref="IsGrantTypeAllowed(GrantTypes)"/>, not with
    ///     <see cref="Enum.HasFlag(Enum)"/>, which returns <see langword="true"/> for <see cref="GrantTypes.None"/>.
    /// </summary>
    public GrantTypes AllowedGrantTypes { get; private set; } = DefaultGrantTypes;

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
    ///     Creates a new enabled Client with the default Grant Types and token lifetimes,
    ///     and without Secrets, Redirect URIs or Scopes.
    /// </summary>
    /// <param name="name">The Display Name of the Client.</param>
    /// <param name="type">The Type of the Client. Cannot be changed later.</param>
    /// <returns>The created Client.</returns>
    /// <exception cref="ArgumentException">Thrown when the Name is empty or too long, or the Type is unknown.</exception>
    public static Client Create(string name, ClientType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException($"Client Type '{type}' is unknown.", nameof(type));
        }

        var client = new Client
        {
            Type = type
        };

        client.SetName(name);

        return client;
    }

    /// <summary>
    ///     Sets the Display Name of the Client.
    /// </summary>
    /// <param name="name">The new Name of the Client.</param>
    /// <exception cref="ArgumentException">Thrown when the Name is empty or too long.</exception>
    public void SetName(string name)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException($"Name must not be longer than {NameMaxLength} characters.", nameof(name));
        }

        Name = trimmed;
    }

    /// <summary>
    ///     Sets the lifetimes of the Access and Refresh Tokens issued to the Client.
    /// </summary>
    /// <param name="accessTokenTTL">The lifetime of Access Tokens in seconds, at most <see cref="MaxAccessTokenTTL"/>.</param>
    /// <param name="refreshTokenTTL">The lifetime of Refresh Tokens in seconds, at most <see cref="MaxRefreshTokenTTL"/>. Must be longer than the Access Token lifetime.</param>
    /// <exception cref="ArgumentException">Thrown when a lifetime is not positive or exceeds its limit, or the Refresh Token lifetime is not longer than the Access Token lifetime.</exception>
    public void SetTokenLifetimes(int accessTokenTTL, int refreshTokenTTL)
    {
        if (accessTokenTTL <= 0)
        {
            throw new DomainValidationException("Access Token TTL must be greater than 0.", nameof(accessTokenTTL));
        }

        if (accessTokenTTL > MaxAccessTokenTTL)
        {
            throw new DomainValidationException($"Access Token TTL must be at most {MaxAccessTokenTTL} seconds.", nameof(accessTokenTTL));
        }

        if (refreshTokenTTL > MaxRefreshTokenTTL)
        {
            throw new DomainValidationException($"Refresh Token TTL must be at most {MaxRefreshTokenTTL} seconds.", nameof(refreshTokenTTL));
        }

        if (refreshTokenTTL <= accessTokenTTL)
        {
            throw new DomainValidationException("Refresh Token TTL must be greater than the Access Token TTL.", nameof(refreshTokenTTL));
        }

        AccessTokenTTL = accessTokenTTL;
        RefreshTokenTTL = refreshTokenTTL;
    }

    /// <summary>
    ///     Sets the Grant Types the Client is allowed to use.
    /// </summary>
    /// <param name="grantTypes">The allowed Grant Types.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when no or unknown Grant Types are given, or <see cref="GrantTypes.RefreshToken"/> is given
    ///     without <see cref="GrantTypes.AuthorizationCode"/>, which is the only way to obtain a first Refresh Token.
    /// </exception>
    public void SetAllowedGrantTypes(GrantTypes grantTypes)
    {
        if (grantTypes == GrantTypes.None)
        {
            throw new DomainValidationException("At least one Grant Type must be allowed.", nameof(grantTypes));
        }

        if ((grantTypes & ~AllGrantTypes) != 0)
        {
            throw new DomainValidationException("Grant Types contain an unknown value.", nameof(grantTypes));
        }

        if (grantTypes.HasFlag(GrantTypes.RefreshToken) && !grantTypes.HasFlag(GrantTypes.AuthorizationCode))
        {
            throw new DomainValidationException("The Refresh Token Grant Type requires the Authorization Code Grant Type.", nameof(grantTypes));
        }

        AllowedGrantTypes = grantTypes;
    }

    /// <summary>
    ///     Checks whether the Client is allowed to use a single requested Grant Type, e.g. at the /token endpoint.
    /// </summary>
    /// <param name="grantType">The requested Grant Type, e.g. from <see cref="GrantTypeNames.TryParse(string?, out GrantTypes)"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if the Grant Type is allowed. Always <see langword="false"/> for <see cref="GrantTypes.None"/>
    ///     (an unknown <c>grant_type</c>) and for combined values, because a request uses exactly one Grant Type.
    /// </returns>
    public bool IsGrantTypeAllowed(GrantTypes grantType)
    {
        return int.IsPow2((int)grantType) && (AllowedGrantTypes & grantType) == grantType;
    }

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
    /// <param name="now">The current point in time (UTC), used as the creation time.</param>
    /// <param name="expiresAt">
    ///     Point in time (UTC) when the Secret expires, or <see langword="null"/> for <see cref="ClientSecret.DefaultLifetime"/>.
    /// </param>
    /// <returns>The created Secret.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Client is not a Confidential Client.</exception>
    /// <exception cref="DomainValidationException">
    ///     Thrown when the expiry is not in the future or later than <see cref="ClientSecret.MaxLifetime"/> from now.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a point in time is not in UTC.</exception>
    public ClientSecret AddSecret(SecretHash secretHash, DateTime now, DateTime? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(secretHash);
        UtcGuard.EnsureUtc(now);

        if (Type != ClientType.Confidential)
        {
            throw new InvalidOperationException("Only confidential clients can have secrets.");
        }

        var effectiveExpiresAt = expiresAt ?? now + ClientSecret.DefaultLifetime;
        UtcGuard.EnsureUtc(effectiveExpiresAt, nameof(expiresAt));

        if (effectiveExpiresAt <= now)
        {
            throw new DomainValidationException("The expiry of the Secret must be in the future.", nameof(expiresAt));
        }

        if (effectiveExpiresAt > now + ClientSecret.MaxLifetime)
        {
            throw new DomainValidationException(
                $"The Secret must expire within {ClientSecret.MaxLifetime.TotalDays} days.",
                nameof(expiresAt));
        }

        var secret = new ClientSecret { SecretHash = secretHash, CreatedAt = now, ExpiresAt = effectiveExpiresAt };
        _secrets.Add(secret);

        return secret;
    }

    /// <summary>
    ///     Revokes a Secret of the Client. Does nothing if the Secret does not exist or is already revoked.
    /// </summary>
    /// <param name="secretId">The Id of the Secret to revoke.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public void RevokeSecret(Guid secretId, DateTime now)
    {
        UtcGuard.EnsureUtc(now);

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
