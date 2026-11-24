// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.RegularExpressions;

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     An external OpenID Connect provider, e.g. Google, through which Users can sign in to Authora. Authora is a
///     Relying Party of the provider: it sends the User there, receives an Authorization Code and verifies the ID Token,
///     then signs the User in to an Authora Session. Clients of Authora see no difference.
/// </summary>
/// <remarks>
///     <para>
///         The Client Secret Authora was given by the provider cannot be hashed, because it must be sent to the
///         provider. It is only stored encrypted (<see cref="ProtectedClientSecret"/>, see
///         <c>IProviderSecretProtector</c>) and never returned.
///     </para>
///     <para>
///         The <see cref="Slug"/> is part of the Redirect URI registered at the provider, so it cannot be changed.
///     </para>
/// </remarks>
public sealed partial class IdentityProvider
{
    /// <summary>
    ///     Maximum length of the Name.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    ///     Maximum length of the Slug.
    /// </summary>
    public const int SlugMaxLength = 50;

    /// <summary>
    ///     Regular expression the Slug must match: lowercase letters, digits and hyphens, starting and ending with a
    ///     letter or digit.
    /// </summary>
    public const string SlugPattern = "^[a-z0-9](?:[a-z0-9-]{0,48}[a-z0-9])?$";

    /// <summary>
    ///     Maximum length of the Issuer.
    /// </summary>
    public const int IssuerMaxLength = 500;

    /// <summary>
    ///     Maximum length of the Client Id at the provider.
    /// </summary>
    public const int ClientIdMaxLength = 500;

    /// <summary>
    ///     Maximum length of a Client Secret at the provider, in plain text.
    /// </summary>
    public const int ClientSecretMaxLength = 500;

    /// <summary>
    ///     Maximum length of the encrypted Client Secret.
    /// </summary>
    public const int ProtectedClientSecretMaxLength = 2000;

    /// <summary>
    ///     Maximum length of the space-delimited Scopes.
    /// </summary>
    public const int ScopesMaxLength = 500;

    /// <summary>
    ///     The Scopes requested from a provider by default.
    /// </summary>
    public const string DefaultScopes = "openid email profile";

    /// <summary>
    ///     The Issuer of the <see cref="IdentityProviderPreset.Google"/> preset.
    /// </summary>
    public const string GoogleIssuer = "https://accounts.google.com";

    /// <summary>
    ///     Used by EF Core to materialize an identity provider from the database.
    /// </summary>
    private IdentityProvider()
    {
    }

    /// <summary>
    ///     Id of the identity provider.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     The Name shown on the sign-in button, e.g. "Google".
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     Unique identifier in URLs, e.g. <c>google</c> in <c>/account/external/google</c>. Cannot be changed, because
    ///     it is part of the Redirect URI registered at the provider.
    /// </summary>
    public string Slug { get; private set; } = null!;

    /// <summary>
    ///     The kind of provider. Fixed at creation.
    /// </summary>
    public IdentityProviderPreset Preset { get; private set; }

    /// <summary>
    ///     The Issuer Identifier of the provider, e.g. <c>https://accounts.google.com</c>. Its discovery document is at
    ///     <c>{Issuer}/.well-known/openid-configuration</c>, and ID Tokens must name exactly this value in <c>iss</c>.
    /// </summary>
    public string Issuer { get; private set; } = null!;

    /// <summary>
    ///     The Client Id of Authora at the provider.
    /// </summary>
    public string ClientId { get; private set; } = null!;

    /// <summary>
    ///     The Client Secret of Authora at the provider, encrypted, or <see langword="null"/> if Authora is a Public
    ///     Client there and authenticates with PKCE only.
    /// </summary>
    public string? ProtectedClientSecret { get; private set; }

    /// <summary>
    ///     The space-delimited Scopes requested from the provider. Always contains <c>openid</c>.
    /// </summary>
    public string Scopes { get; private set; } = DefaultScopes;

    /// <summary>
    ///     Whether Users can sign in through the provider. A disabled provider is not shown on the sign-in page and its
    ///     flows are rejected, but the linked identities are kept.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    ///     Position of the button on the sign-in page; lower values first, equal values by Name.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the identity provider was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the identity provider was last modified, or <see langword="null"/>.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Whether a Client Secret is set.
    /// </summary>
    public bool HasClientSecret => ProtectedClientSecret is not null;

    /// <summary>
    ///     The Scopes requested from the provider.
    /// </summary>
    public IReadOnlyList<string> ScopeList => Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Creates a new identity provider, disabled until <see cref="Enable"/> is called.
    /// </summary>
    /// <param name="name">The Name shown on the sign-in button.</param>
    /// <param name="slug">The unique identifier in URLs, see <see cref="SlugPattern"/>.</param>
    /// <param name="preset">The kind of provider.</param>
    /// <param name="issuer">The Issuer; ignored for <see cref="IdentityProviderPreset.Google"/>.</param>
    /// <param name="clientId">The Client Id of Authora at the provider.</param>
    /// <param name="scopes">The space-delimited Scopes, or null for <see cref="DefaultScopes"/>.</param>
    /// <param name="sortOrder">The position of the button on the sign-in page.</param>
    /// <returns>The created identity provider.</returns>
    /// <exception cref="DomainValidationException">Thrown when a value is missing or invalid.</exception>
    public static IdentityProvider Create(
        string name,
        string slug,
        IdentityProviderPreset preset,
        string? issuer,
        string clientId,
        string? scopes = null,
        int sortOrder = 0)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(slug);

        var trimmedSlug = slug.Trim();

        if (!IsValidSlug(trimmedSlug))
        {
            throw new DomainValidationException(
                $"Slug must be at most {SlugMaxLength} characters of lowercase letters, digits and hyphens, and start and end with a letter or digit.",
                nameof(slug));
        }

        if (!Enum.IsDefined(preset))
        {
            throw new DomainValidationException("The preset is unknown.", nameof(preset));
        }

        var provider = new IdentityProvider
        {
            Slug = trimmedSlug,
            Preset = preset
        };

        provider.SetName(name);
        provider.SetIssuer(preset == IdentityProviderPreset.Google ? GoogleIssuer : issuer);
        provider.SetClientId(clientId);
        provider.SetScopes(scopes ?? DefaultScopes);
        provider.SetSortOrder(sortOrder);

        return provider;
    }

    /// <summary>
    ///     Checks whether a Slug is valid, see <see cref="SlugPattern"/>.
    /// </summary>
    /// <param name="slug">The Slug.</param>
    /// <returns>True if the Slug is valid.</returns>
    public static bool IsValidSlug(string? slug)
    {
        return !string.IsNullOrEmpty(slug) && SlugRegex().IsMatch(slug);
    }

    /// <summary>
    ///     Sets the Name shown on the sign-in button.
    /// </summary>
    /// <param name="name">The new Name.</param>
    /// <exception cref="DomainValidationException">Thrown when the Name is empty or too long.</exception>
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
    ///     Sets the Issuer. The Issuer of the <see cref="IdentityProviderPreset.Google"/> preset cannot be changed.
    /// </summary>
    /// <param name="issuer">An absolute <c>https</c> (or, for development, <c>http</c>) URL without query and fragment.</param>
    /// <exception cref="DomainValidationException">Thrown when the Issuer is invalid or the preset fixes it.</exception>
    public void SetIssuer(string? issuer)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(issuer);

        var trimmed = issuer.Trim();

        if (Preset == IdentityProviderPreset.Google && !string.Equals(trimmed, GoogleIssuer, StringComparison.Ordinal))
        {
            throw new DomainValidationException($"The Issuer of the Google preset is always '{GoogleIssuer}'.", nameof(issuer));
        }

        if (trimmed.Length > IssuerMaxLength
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || trimmed.Contains('?', StringComparison.Ordinal)
            || trimmed.Contains('#', StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                $"Issuer must be an absolute https URL of at most {IssuerMaxLength} characters without query and fragment.",
                nameof(issuer));
        }

        Issuer = trimmed;
    }

    /// <summary>
    ///     Sets the Client Id of Authora at the provider.
    /// </summary>
    /// <param name="clientId">The Client Id.</param>
    /// <exception cref="DomainValidationException">Thrown when the Client Id is empty or too long.</exception>
    public void SetClientId(string clientId)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(clientId);

        var trimmed = clientId.Trim();

        if (trimmed.Length > ClientIdMaxLength)
        {
            throw new DomainValidationException($"Client Id must not be longer than {ClientIdMaxLength} characters.", nameof(clientId));
        }

        ClientId = trimmed;
    }

    /// <summary>
    ///     Sets the Scopes requested from the provider. <c>openid</c> is added if it is missing.
    /// </summary>
    /// <param name="scopes">The space-delimited Scopes.</param>
    /// <exception cref="DomainValidationException">Thrown when a Scope is invalid or the value is too long.</exception>
    public void SetScopes(string scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var names = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Prepend("openid")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!names.All(Scope.IsValidName))
        {
            throw new DomainValidationException(
                "Scopes must be space-delimited printable ASCII characters except '\"' and '\\'.",
                nameof(scopes));
        }

        var value = string.Join(' ', names);

        if (value.Length > ScopesMaxLength)
        {
            throw new DomainValidationException($"Scopes must not be longer than {ScopesMaxLength} characters.", nameof(scopes));
        }

        Scopes = value;
    }

    /// <summary>
    ///     Sets the position of the button on the sign-in page.
    /// </summary>
    /// <param name="sortOrder">The position; lower values first.</param>
    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    /// <summary>
    ///     Sets or removes the encrypted Client Secret.
    /// </summary>
    /// <param name="protectedClientSecret">The encrypted Client Secret, or null to remove it.</param>
    /// <exception cref="ArgumentException">Thrown when the encrypted value is empty or too long.</exception>
    public void SetProtectedClientSecret(string? protectedClientSecret)
    {
        if (protectedClientSecret is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedClientSecret);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(protectedClientSecret.Length, ProtectedClientSecretMaxLength);
        }

        ProtectedClientSecret = protectedClientSecret;
    }

    /// <summary>
    ///     Enables the provider, so Users can sign in through it.
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
    }

    /// <summary>
    ///     Disables the provider. Linked identities are kept, but cannot be used until it is enabled again.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
    }

    [GeneratedRegex(SlugPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
