// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     A passkey of a User: a WebAuthn public key credential, used as a phishing-resistant second factor after the
///     password.
/// </summary>
/// <remarks>
///     <para>
///         Only the public key is stored; the private key never leaves the authenticator. A User can register several,
///         e.g. one on the phone and a security key as a backup, each with a name of their choice.
///     </para>
///     <para>
///         <see cref="SignCount"/> is the signature counter the authenticator reported last. A response with a counter
///         that did not increase is rejected, because it may come from a cloned authenticator. Authenticators that do
///         not count (most synced passkeys) always report 0, which is accepted.
///     </para>
///     <para>
///         A passkey that may be backed up (<see cref="IsBackupEligible"/>), e.g. synced by a password manager or the
///         platform, records <c>swk</c> (software-secured key) in the Session, a device-bound one <c>hwk</c>
///         (hardware-secured key), see <see cref="AuthenticationMethod"/>.
///     </para>
/// </remarks>
public sealed class Passkey
{
    /// <summary>
    ///     Maximum length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 64;

    /// <summary>
    ///     Maximum length of <see cref="CredentialId"/> in bytes (WebAuthn Level 2, Section 4).
    /// </summary>
    public const int CredentialIdMaxLength = 1023;

    /// <summary>
    ///     Maximum length of <see cref="PublicKey"/> in bytes. COSE keys of the supported algorithms are much shorter;
    ///     an RSA key with 4096 bits takes about 550.
    /// </summary>
    public const int PublicKeyMaxLength = 2048;

    /// <summary>
    ///     Maximum length of <see cref="Transports"/>.
    /// </summary>
    public const int TransportsMaxLength = 200;

    /// <summary>
    ///     How many passkeys a User can register.
    /// </summary>
    public const int MaxPerUser = 20;

    /// <summary>
    ///     Used by EF Core to materialize a passkey from the database.
    /// </summary>
    private Passkey()
    {
    }

    /// <summary>
    ///     Id of the passkey.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="User"/> it belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     The name the User gave it, e.g. "Phone".
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     The credential Id the authenticator assigned. Unique among all passkeys.
    /// </summary>
    public byte[] CredentialId { get; private set; } = null!;

    /// <summary>
    ///     The public key, COSE-encoded (RFC 9052), which verifies the signatures of the passkey.
    /// </summary>
    public byte[] PublicKey { get; private set; } = null!;

    /// <summary>
    ///     The signature counter the authenticator reported last, 0 if it does not count.
    /// </summary>
    public long SignCount { get; private set; }

    /// <summary>
    ///     The AAGUID of the authenticator model, or <see cref="Guid.Empty"/> if the authenticator does not reveal it.
    /// </summary>
    public Guid AaGuid { get; private set; }

    /// <summary>
    ///     How the browser can reach the authenticator, e.g. <c>usb</c> or <c>internal</c>, space-delimited as the
    ///     browser reported them at registration.
    /// </summary>
    public string Transports { get; private set; } = string.Empty;

    /// <summary>
    ///     Whether the passkey may be backed up or synced to other devices (the BE flag of WebAuthn Level 3). Fixed for
    ///     the lifetime of the passkey.
    /// </summary>
    public bool IsBackupEligible { get; private set; }

    /// <summary>
    ///     Whether the passkey was backed up when it was last used (the BS flag of WebAuthn Level 3).
    /// </summary>
    public bool IsBackedUp { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the User registered it.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when it was last used to sign in, or <see langword="null"/> if it never was.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    ///     The Authentication Method Reference value (RFC 8176) a Session records when the passkey is used: <c>swk</c>
    ///     if it may be backed up, otherwise <c>hwk</c>.
    /// </summary>
    public string AuthenticationMethod => IsBackupEligible ? AuthenticationMethods.SoftwareKey : AuthenticationMethods.HardwareKey;

    /// <summary>
    ///     The transports as a list.
    /// </summary>
    public IReadOnlyList<string> TransportList => Transports.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Creates a passkey from a verified registration.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="name">The name the User gave it, see <see cref="NormalizeName"/>.</param>
    /// <param name="credentialId">The credential Id.</param>
    /// <param name="publicKey">The COSE-encoded public key.</param>
    /// <param name="signCount">The signature counter of the registration.</param>
    /// <param name="aaGuid">The AAGUID of the authenticator model.</param>
    /// <param name="transports">The transports the browser reported, e.g. <c>usb</c>. Values other than lowercase letters and hyphens are ignored.</param>
    /// <param name="isBackupEligible">Whether it may be backed up.</param>
    /// <param name="isBackedUp">Whether it is backed up.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The passkey.</returns>
    /// <exception cref="DomainValidationException">Thrown when the name is empty or too long.</exception>
    /// <exception cref="ArgumentException">Thrown when another value is missing or invalid.</exception>
    public static Passkey Create(
        Guid userId,
        string name,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        Guid aaGuid,
        IEnumerable<string> transports,
        bool isBackupEligible,
        bool isBackedUp,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(transports);
        UtcGuard.EnsureUtc(now);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The User is required.", nameof(userId));
        }

        if (credentialId.Length is 0 or > CredentialIdMaxLength)
        {
            throw new ArgumentException($"The credential Id must have 1 to {CredentialIdMaxLength} bytes.", nameof(credentialId));
        }

        if (publicKey.Length is 0 or > PublicKeyMaxLength)
        {
            throw new ArgumentException($"The public key must have 1 to {PublicKeyMaxLength} bytes.", nameof(publicKey));
        }

        return new Passkey
        {
            UserId = userId,
            Name = NormalizeName(name),
            CredentialId = [.. credentialId],
            PublicKey = [.. publicKey],
            SignCount = signCount,
            AaGuid = aaGuid,
            Transports = FormatTransports(transports),
            IsBackupEligible = isBackupEligible,
            IsBackedUp = isBackedUp,
            CreatedAt = now
        };
    }

    /// <summary>
    ///     Checks and trims a name: not empty, at most <see cref="NameMaxLength"/> characters and without control
    ///     characters.
    /// </summary>
    /// <param name="name">The name the User entered.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="DomainValidationException">Thrown when the name is invalid.</exception>
    public static string NormalizeName(string? name)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException($"The name must be at most {NameMaxLength} characters long.", nameof(name));
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new DomainValidationException("The name must not contain control characters.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>
    ///     Gives the passkey a new name.
    /// </summary>
    /// <param name="name">The new name, see <see cref="NormalizeName"/>.</param>
    /// <exception cref="DomainValidationException">Thrown when the name is invalid.</exception>
    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    private static string FormatTransports(IEnumerable<string> transports)
    {
        var valid = transports
            .Where(transport => !string.IsNullOrEmpty(transport)
                && transport.Length <= 32
                && transport.All(character => char.IsAsciiLetterLower(character) || character == '-'))
            .Distinct(StringComparer.Ordinal);

        var value = string.Empty;

        foreach (var transport in valid)
        {
            var next = value.Length == 0 ? transport : $"{value} {transport}";

            if (next.Length > TransportsMaxLength)
            {
                break;
            }

            value = next;
        }

        return value;
    }
}
