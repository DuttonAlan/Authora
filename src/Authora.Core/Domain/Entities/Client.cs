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
    ///     Maximum length of the <see cref="BackchannelLogoutUri"/>.
    /// </summary>
    public const int BackchannelLogoutUriMaxLength = 2048;

    /// <summary>
    ///     Maximum length of the <see cref="ResourceIdentifier"/>.
    /// </summary>
    public const int ResourceIdentifierMaxLength = 2048;

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

    private const GrantTypes AllGrantTypes = GrantTypes.AuthorizationCode | GrantTypes.RefreshToken | GrantTypes.ClientCredentials;

    private readonly List<ClientSecret> _secrets = [];
    private readonly List<RedirectUri> _redirectUris = [];
    private readonly List<RedirectUri> _postLogoutRedirectUris = [];
    private readonly List<Scope> _allowedScopes = [];
    private readonly List<Scope> _servedScopes = [];

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
    ///     Registered Post-logout Redirect URIs. The <c>post_logout_redirect_uri</c> of a logout request (OpenID Connect
    ///     RP-Initiated Logout 1.0) must exactly match one of them.
    /// </summary>
    public IReadOnlyCollection<RedirectUri> PostLogoutRedirectUris => _postLogoutRedirectUris;

    /// <summary>
    ///     Scopes the Client is allowed to request. Requested Scopes that are not in this list are rejected.
    /// </summary>
    public IReadOnlyCollection<Scope> AllowedScopes => _allowedScopes;

    /// <summary>
    ///     Scopes the Client serves as a resource server. It may introspect Access Tokens of other Clients only if it is
    ///     in their audience (<see cref="AccessToken.Audience"/>), and then sees only these Scopes. A token requested
    ///     without <c>resource</c> is meant for every Client that serves one of its Scopes. Only Confidential Clients
    ///     serve Scopes.
    /// </summary>
    public IReadOnlyCollection<Scope> ServedScopes => _servedScopes;

    /// <summary>
    ///     Status of the Client. A disabled Client cannot obtain new tokens.
    /// </summary>
    public ClientStatus Status { get; private set; } = ClientStatus.Enabled;

    /// <summary>
    ///     Whether the Client is part of the same deployment as Authora, e.g. the admin interface. Users are not asked
    ///     for consent when a first-party Client requests Scopes it is allowed, unless it sends <c>prompt=consent</c>.
    ///     Defaults to <see langword="false"/>. Set via <see cref="SetFirstParty(bool)"/>.
    /// </summary>
    public bool IsFirstParty { get; private set; }

    /// <summary>
    ///     Whether the Client may send authorization requests without PKCE. Defaults to <see langword="false"/>: every
    ///     request must contain a <c>code_challenge</c>. Only Confidential Clients can be exempted (OAuth 2.1,
    ///     Section 7.5.1), for OpenID Connect Clients that protect the code with the <c>nonce</c> instead. A request
    ///     that does send PKCE is still checked. Set via <see cref="SetPkceOptional(bool)"/>.
    /// </summary>
    public bool IsPkceOptional { get; private set; }

    /// <summary>
    ///     Whether authorization requests of the Client must contain PKCE: always for Public Clients, and for
    ///     Confidential Clients unless <see cref="IsPkceOptional"/> is set.
    /// </summary>
    public bool RequiresPkce => Type == ClientType.Public || !IsPkceOptional;

    /// <summary>
    ///     Whether Users must sign in with multi-factor authentication to use the Client. Defaults to
    ///     <see langword="false"/>. A Session without a second factor is stepped up before the Client gets a code, and
    ///     Users without one set it up first. Set via <see cref="SetRequiresMfa(bool)"/>.
    /// </summary>
    public bool RequiresMfa { get; private set; }

    /// <summary>
    ///     The Back-Channel Logout URI of the Client (OpenID Connect Back-Channel Logout 1.0), to which Authora sends a
    ///     Logout Token when a Session ends in which the Client received tokens, or <see langword="null"/> if the Client
    ///     is not notified. Set via <see cref="SetBackchannelLogoutUri(string?, bool)"/>.
    /// </summary>
    public string? BackchannelLogoutUri { get; private set; }

    /// <summary>
    ///     Whether the Client requires the <c>sid</c> claim in Logout Tokens (<c>backchannel_logout_session_required</c>).
    ///     Authora always sends it, so this only records what the Client expects. Defaults to <see langword="false"/>.
    ///     Set via <see cref="SetBackchannelLogoutSessionRequired(bool)"/>.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; private set; }

    /// <summary>
    ///     The Resource Identifier of the Client (RFC 8707), e.g. <c>https://api.example.com</c>, or
    ///     <see langword="null"/>. A Client with one is an <em>API</em>: Clients name it in the <c>resource</c>
    ///     parameter to get Access Tokens meant for it. Only Confidential Clients can have one, and it is unique among
    ///     all Clients. Set via <see cref="SetResourceIdentifier(string?, bool)"/>.
    /// </summary>
    public string? ResourceIdentifier { get; private set; }

    /// <summary>
    ///     Whether Access Tokens are only meant for the API if the <c>resource</c> parameter named it. Defaults to
    ///     <see langword="false"/>: a token requested without <c>resource</c> is meant for every Client that serves one
    ///     of its Scopes. Requires a <see cref="ResourceIdentifier"/>. Set via
    ///     <see cref="SetRequiresResourceIndicator(bool)"/>.
    /// </summary>
    public bool RequiresResourceIndicator { get; private set; }

    /// <summary>
    ///     Whether the Client is an API, i.e. has a <see cref="ResourceIdentifier"/>.
    /// </summary>
    public bool IsApi => ResourceIdentifier is not null;

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
    ///     without <see cref="GrantTypes.AuthorizationCode"/>, which is the only way to obtain a first Refresh Token, or
    ///     <see cref="GrantTypes.ClientCredentials"/> is given for a Public Client, which cannot authenticate.
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

        // A Public Client has no Secret, so anyone who knows its client_id could obtain tokens for it.
        if (grantTypes.HasFlag(GrantTypes.ClientCredentials) && Type != ClientType.Confidential)
        {
            throw new DomainValidationException("Only Confidential Clients can use the Client Credentials Grant Type.", nameof(grantTypes));
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
    ///     Marks the Client as first-party, so Users are no longer asked for consent, or as third-party again.
    /// </summary>
    /// <param name="isFirstParty">True to skip the consent screen for the Client.</param>
    public void SetFirstParty(bool isFirstParty)
    {
        IsFirstParty = isFirstParty;
    }

    /// <summary>
    ///     Lets the Client send authorization requests without PKCE, or requires PKCE again.
    /// </summary>
    /// <param name="isPkceOptional">True to accept requests without a <c>code_challenge</c>.</param>
    /// <exception cref="DomainValidationException">
    ///     Thrown when PKCE should become optional for a Public Client: without a Secret, PKCE is the only protection
    ///     of its Authorization Codes.
    /// </exception>
    public void SetPkceOptional(bool isPkceOptional)
    {
        if (isPkceOptional && Type != ClientType.Confidential)
        {
            throw new DomainValidationException("PKCE can only be made optional for Confidential Clients.", nameof(isPkceOptional));
        }

        IsPkceOptional = isPkceOptional;
    }

    /// <summary>
    ///     Requires multi-factor authentication for the Users of the Client, or no longer requires it.
    /// </summary>
    /// <param name="requiresMfa">True to require at least two factors.</param>
    public void SetRequiresMfa(bool requiresMfa)
    {
        RequiresMfa = requiresMfa;
    }

    /// <summary>
    ///     Sets the Back-Channel Logout URI, or removes it.
    /// </summary>
    /// <param name="uri">The absolute URI, or <see langword="null"/> or empty to stop notifying the Client.</param>
    /// <param name="allowHttp">
    ///     Whether plain HTTP is allowed, e.g. for development. Otherwise the URI must use HTTPS, because the Logout
    ///     Token identifies the User.
    /// </param>
    /// <exception cref="DomainValidationException">
    ///     Thrown when the URI is not absolute, too long, contains a fragment, user information or whitespaces, or does
    ///     not use HTTPS (or HTTP, if allowed).
    /// </exception>
    public void SetBackchannelLogoutUri(string? uri, bool allowHttp)
    {
        if (string.IsNullOrEmpty(uri))
        {
            BackchannelLogoutUri = null;
            return;
        }

        if (uri.Length > BackchannelLogoutUriMaxLength)
        {
            throw new DomainValidationException(
                $"Back-Channel Logout URI must not be longer than {BackchannelLogoutUriMaxLength} characters.",
                nameof(uri));
        }

        if (uri != uri.Trim() || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            throw new DomainValidationException("Back-Channel Logout URI must be absolute, without whitespaces.", nameof(uri));
        }

        // OpenID Connect Back-Channel Logout 1.0, Section 2.2: the URI must not include a fragment.
        if (!string.IsNullOrEmpty(parsed.Fragment) || uri.Contains('#', StringComparison.Ordinal))
        {
            throw new DomainValidationException("Back-Channel Logout URI must not contain a fragment.", nameof(uri));
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            throw new DomainValidationException("Back-Channel Logout URI must not contain user information.", nameof(uri));
        }

        if (parsed.Scheme != Uri.UriSchemeHttps && !(allowHttp && parsed.Scheme == Uri.UriSchemeHttp))
        {
            throw new DomainValidationException(
                allowHttp ? "Back-Channel Logout URI must use HTTP or HTTPS." : "Back-Channel Logout URI must use HTTPS.",
                nameof(uri));
        }

        BackchannelLogoutUri = uri;
    }

    /// <summary>
    ///     Records whether the Client requires the <c>sid</c> claim in Logout Tokens.
    /// </summary>
    /// <param name="sessionRequired">True if the Client requires <c>sid</c>.</param>
    public void SetBackchannelLogoutSessionRequired(bool sessionRequired)
    {
        BackchannelLogoutSessionRequired = sessionRequired;
    }

    /// <summary>
    ///     Sets the Resource Identifier (RFC 8707), which makes the Client an API, or removes it. Removing it also
    ///     removes <see cref="RequiresResourceIndicator"/>.
    /// </summary>
    /// <remarks>
    ///     Neither uniqueness nor the identifier reserved for Authora's own admin API can be checked here; the caller
    ///     checks both.
    /// </remarks>
    /// <param name="identifier">The absolute URI, or <see langword="null"/> or empty to remove it.</param>
    /// <param name="allowHttp">Whether plain HTTP is allowed, e.g. for development. Otherwise the URI must use HTTPS.</param>
    /// <exception cref="DomainValidationException">
    ///     Thrown when the Client is not a Confidential Client, or the URI is not absolute, too long, contains a query,
    ///     a fragment, user information or whitespaces, or does not use HTTPS (or HTTP, if allowed).
    /// </exception>
    public void SetResourceIdentifier(string? identifier, bool allowHttp)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            ResourceIdentifier = null;
            RequiresResourceIndicator = false;
            return;
        }

        // An API checks tokens at the introspection endpoint, where only Confidential Clients can authenticate.
        if (Type != ClientType.Confidential)
        {
            throw new DomainValidationException("Only Confidential Clients can have a Resource Identifier.", nameof(identifier));
        }

        if (identifier.Length > ResourceIdentifierMaxLength)
        {
            throw new DomainValidationException(
                $"Resource Identifier must not be longer than {ResourceIdentifierMaxLength} characters.",
                nameof(identifier));
        }

        if (identifier != identifier.Trim()
            || identifier.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(identifier, UriKind.Absolute, out var parsed))
        {
            throw new DomainValidationException("Resource Identifier must be an absolute URI, without whitespaces.", nameof(identifier));
        }

        // RFC 8707, Section 2: the URI must not include a fragment and should not include a query.
        if (!string.IsNullOrEmpty(parsed.Fragment) || identifier.Contains('#', StringComparison.Ordinal))
        {
            throw new DomainValidationException("Resource Identifier must not contain a fragment.", nameof(identifier));
        }

        if (!string.IsNullOrEmpty(parsed.Query) || identifier.Contains('?', StringComparison.Ordinal))
        {
            throw new DomainValidationException("Resource Identifier must not contain a query.", nameof(identifier));
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            throw new DomainValidationException("Resource Identifier must not contain user information.", nameof(identifier));
        }

        if (parsed.Scheme != Uri.UriSchemeHttps && !(allowHttp && parsed.Scheme == Uri.UriSchemeHttp))
        {
            throw new DomainValidationException(
                allowHttp ? "Resource Identifier must use HTTP or HTTPS." : "Resource Identifier must use HTTPS.",
                nameof(identifier));
        }

        ResourceIdentifier = identifier;
    }

    /// <summary>
    ///     Makes Access Tokens visible to the API only if the <c>resource</c> parameter named it, or also when it was
    ///     omitted again.
    /// </summary>
    /// <param name="requiresResourceIndicator">True to require the <c>resource</c> parameter.</param>
    /// <exception cref="DomainValidationException">Thrown when it should be required for a Client without Resource Identifier.</exception>
    public void SetRequiresResourceIndicator(bool requiresResourceIndicator)
    {
        if (requiresResourceIndicator && ResourceIdentifier is null)
        {
            throw new DomainValidationException(
                "Only a Client with a Resource Identifier can require the resource parameter.",
                nameof(requiresResourceIndicator));
        }

        RequiresResourceIndicator = requiresResourceIndicator;
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
    ///     Registers a new Post-logout Redirect URI. Does nothing if the URI is already registered.
    /// </summary>
    /// <param name="uri">The Post-logout Redirect URI to register. Same rules as for Redirect URIs.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is not a valid Redirect URI.</exception>
    public void AddPostLogoutRedirectUri(string uri)
    {
        var redirectUri = RedirectUri.Create(uri);

        if (!_postLogoutRedirectUris.Contains(redirectUri))
        {
            _postLogoutRedirectUris.Add(redirectUri);
        }
    }

    /// <summary>
    ///     Removes a registered Post-logout Redirect URI. Does nothing if the URI is not registered.
    /// </summary>
    /// <param name="uri">The Post-logout Redirect URI to remove.</param>
    public void RemovePostLogoutRedirectUri(string uri)
    {
        _postLogoutRedirectUris.RemoveAll(x => x.Matches(uri));
    }

    /// <summary>
    ///     Checks whether the requested Post-logout Redirect URI exactly matches one of the registered ones.
    /// </summary>
    /// <param name="requested">The <c>post_logout_redirect_uri</c> from the logout request.</param>
    /// <returns><see langword="true"/> if the URI is registered, otherwise <see langword="false"/>.</returns>
    public bool IsPostLogoutRedirectUriAllowed(string requested)
    {
        return _postLogoutRedirectUris.Any(x => x.Matches(requested));
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
    ///     Makes the Client serve a Scope as a resource server. Does nothing if the Scope is already served.
    /// </summary>
    /// <remarks>
    ///     <see cref="ServedScopes"/> must be loaded, otherwise the duplicate check cannot work.
    /// </remarks>
    /// <param name="scope">The Scope to serve.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the Client is not a Confidential Client, or the Scope is a System Scope.
    /// </exception>
    public void ServeScope(Scope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Only Confidential Clients can authenticate at the introspection endpoint.
        if (Type != ClientType.Confidential)
        {
            throw new InvalidOperationException("Only confidential clients can serve scopes.");
        }

        // System Scopes are served by Authora itself (admin API, UserInfo). A Client serving e.g. openid would see
        // every sign-in of every Client as an active token at introspection.
        if (scope.IsSystem)
        {
            throw new InvalidOperationException("System scopes cannot be served by clients.");
        }

        if (!_servedScopes.Any(x => x.Id == scope.Id))
        {
            _servedScopes.Add(scope);
        }
    }

    /// <summary>
    ///     Stops the Client from serving a Scope. Does nothing if the Scope is not served.
    /// </summary>
    /// <param name="scopeId">The Id of the Scope.</param>
    public void StopServingScope(Guid scopeId)
    {
        _servedScopes.RemoveAll(x => x.Id == scopeId);
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
