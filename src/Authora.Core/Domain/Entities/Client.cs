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
    ///     Combine multiple values with <c>|</c> and check them with <see cref="Enum.HasFlag(Enum)"/>.
    ///     Set via <see cref="SetAllowedGrantTypes(GrantTypes)"/>.
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
            throw new ArgumentException($"Client Type '{type}' is unknown.", nameof(type));
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"Name must not be longer than {NameMaxLength} characters.", nameof(name));
        }

        Name = trimmed;
    }

    /// <summary>
    ///     Sets the lifetimes of the Access and Refresh Tokens issued to the Client.
    /// </summary>
    /// <param name="accessTokenTTL">The lifetime of Access Tokens in seconds.</param>
    /// <param name="refreshTokenTTL">The lifetime of Refresh Tokens in seconds. Must be longer than the Access Token lifetime.</param>
    /// <exception cref="ArgumentException">Thrown when a lifetime is not positive, or the Refresh Token lifetime is not longer than the Access Token lifetime.</exception>
    public void SetTokenLifetimes(int accessTokenTTL, int refreshTokenTTL)
    {
        if (accessTokenTTL <= 0)
        {
            throw new ArgumentException("Access Token TTL must be greater than 0.", nameof(accessTokenTTL));
        }

        if (refreshTokenTTL <= accessTokenTTL)
        {
            throw new ArgumentException("Refresh Token TTL must be greater than the Access Token TTL.", nameof(refreshTokenTTL));
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
            throw new ArgumentException("At least one Grant Type must be allowed.", nameof(grantTypes));
        }

        if ((grantTypes & ~AllGrantTypes) != 0)
        {
            throw new ArgumentException("Grant Types contain an unknown value.", nameof(grantTypes));
        }

        if (grantTypes.HasFlag(GrantTypes.RefreshToken) && !grantTypes.HasFlag(GrantTypes.AuthorizationCode))
        {
            throw new ArgumentException("The Refresh Token Grant Type requires the Authorization Code Grant Type.", nameof(grantTypes));
        }

        AllowedGrantTypes = grantTypes;
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
