// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     The link between a User of Authora and their account at an external <see cref="IdentityProvider"/>, identified
///     by the provider's Subject (<c>sub</c>). A linked User can sign in through the provider.
/// </summary>
/// <remarks>
///     A Subject belongs to at most one User per provider, and a User has at most one identity per provider. The
///     identities of a User are deleted when the User is deleted.
/// </remarks>
public sealed class ExternalIdentity
{
    /// <summary>
    ///     Maximum length of <see cref="Subject"/> (OpenID Connect Core 1.0, Section 2).
    /// </summary>
    public const int SubjectMaxLength = 255;

    /// <summary>
    ///     Used by EF Core to materialize an external identity from the database.
    /// </summary>
    private ExternalIdentity()
    {
    }

    /// <summary>
    ///     Id of the external identity.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="User"/> it belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="IdentityProvider"/>.
    /// </summary>
    public Guid ProviderId { get; private set; }

    /// <summary>
    ///     The Subject Identifier of the account at the provider (<c>sub</c> of its ID Tokens). Case-sensitive, never
    ///     reassigned by the provider.
    /// </summary>
    public string Subject { get; private set; } = null!;

    /// <summary>
    ///     The Email the provider reported when the identity was linked, or <see langword="null"/>. Only for display:
    ///     the provider's Email may change, the Subject does not.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the identity was linked.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the User last signed in through it, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    ///     Links an account at a provider to a User.
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <param name="providerId">The Id of the provider.</param>
    /// <param name="subject">The Subject of the account at the provider.</param>
    /// <param name="email">The Email the provider reported, or null.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The external identity.</returns>
    /// <exception cref="DomainValidationException">Thrown when the Subject is empty or too long.</exception>
    public static ExternalIdentity Create(Guid userId, Guid providerId, string subject, string? email, DateTime now)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(subject);
        UtcGuard.EnsureUtc(now);

        if (subject.Length > SubjectMaxLength)
        {
            throw new DomainValidationException($"Subject must not be longer than {SubjectMaxLength} characters.", nameof(subject));
        }

        // The Email is only shown, so one the provider reports in an unexpected form is dropped instead of failing.
        var trimmedEmail = email?.Trim();

        return new ExternalIdentity
        {
            UserId = userId,
            ProviderId = providerId,
            Subject = subject,
            Email = string.IsNullOrEmpty(trimmedEmail) || trimmedEmail.Length > User.EmailMaxLength ? null : trimmedEmail,
            CreatedAt = now
        };
    }

    /// <summary>
    ///     Records that the User signed in through the identity.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    public void RecordUse(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        LastUsedAt = now;
    }
}
