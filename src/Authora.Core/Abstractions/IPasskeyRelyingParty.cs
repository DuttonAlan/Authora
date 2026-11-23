// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the WebAuthn relying party (WebAuthn Level 2): the options the browser passes to
///     <c>navigator.credentials.create</c> and <c>.get</c>, and the verification of the responses.
/// </summary>
/// <remarks>
///     The relying party Id is the host of the Issuer, and responses are only accepted from the origin of the Issuer.
///     Options and responses are the JSON the browser scripts exchange with the server, binary values base64url-encoded.
///     Implementations do not store anything; challenges, replay and the sign counter are handled by the callers.
/// </remarks>
public interface IPasskeyRelyingParty
{
    /// <summary>
    ///     Creates the options to register a passkey: user verification preferred, no attestation, the existing
    ///     passkeys of the User excluded.
    /// </summary>
    /// <param name="request">The challenge, the User and the passkeys to exclude.</param>
    /// <returns>The <c>PublicKeyCredentialCreationOptions</c> as JSON.</returns>
    public string CreateRegistrationOptions(PasskeyRegistrationRequest request);

    /// <summary>
    ///     Creates the options to sign in with one of the given passkeys, user verification preferred.
    /// </summary>
    /// <param name="challenge">The challenge.</param>
    /// <param name="allowedCredentials">The passkeys of the User.</param>
    /// <returns>The <c>PublicKeyCredentialRequestOptions</c> as JSON.</returns>
    public string CreateAuthenticationOptions(byte[] challenge, IReadOnlyList<PasskeyDescriptor> allowedCredentials);

    /// <summary>
    ///     Reads the challenge and the credential Id from a response, without verifying anything, to look up the
    ///     challenge and the passkey it is for.
    /// </summary>
    /// <param name="responseJson">The response of the browser.</param>
    /// <returns>The challenge and the credential Id, or null if the response is malformed.</returns>
    public PasskeyClientResponse? ReadResponse(string? responseJson);

    /// <summary>
    ///     Verifies the response of a registration.
    /// </summary>
    /// <param name="responseJson">The response of <c>navigator.credentials.create</c>.</param>
    /// <param name="request">The request the options were created from, with the challenge the response must have signed.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new credential, or the reason it was rejected.</returns>
    public Task<PasskeyRegistrationVerification> VerifyRegistrationAsync(
        string responseJson,
        PasskeyRegistrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies the response of a sign-in: the challenge, origin, relying party, user presence and the signature
    ///     with the stored public key.
    /// </summary>
    /// <param name="responseJson">The response of <c>navigator.credentials.get</c>.</param>
    /// <param name="request">The challenge, the stored passkey and its User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new counter and backup state, or the reason it was rejected.</returns>
    public Task<PasskeyAuthenticationVerification> VerifyAuthenticationAsync(
        string responseJson,
        PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     A passkey the browser may use or must not register again.
/// </summary>
/// <param name="CredentialId">The credential Id.</param>
/// <param name="Transports">The transports, e.g. <c>usb</c>.</param>
public sealed record PasskeyDescriptor(byte[] CredentialId, IReadOnlyList<string> Transports);

/// <summary>
///     The registration of a passkey for a User.
/// </summary>
/// <param name="Challenge">The challenge.</param>
/// <param name="UserHandle">The WebAuthn user handle: the Id of the User, which contains nothing personal.</param>
/// <param name="UserName">The name the authenticator shows, the Email of the User.</param>
/// <param name="UserDisplayName">The display name the authenticator shows.</param>
/// <param name="ExcludeCredentials">The passkeys the User already registered.</param>
public sealed record PasskeyRegistrationRequest(
    byte[] Challenge,
    byte[] UserHandle,
    string UserName,
    string UserDisplayName,
    IReadOnlyList<PasskeyDescriptor> ExcludeCredentials);

/// <summary>
///     A sign-in with a stored passkey.
/// </summary>
/// <param name="Challenge">The challenge the response must have signed.</param>
/// <param name="UserHandle">The user handle of the User, which the response must name if it names one.</param>
/// <param name="CredentialId">The credential Id of the stored passkey.</param>
/// <param name="PublicKey">The COSE-encoded public key of the stored passkey.</param>
/// <param name="SignCount">The signature counter stored for the passkey.</param>
public sealed record PasskeyAuthenticationRequest(
    byte[] Challenge,
    byte[] UserHandle,
    byte[] CredentialId,
    byte[] PublicKey,
    uint SignCount);

/// <summary>
///     What <see cref="IPasskeyRelyingParty.ReadResponse"/> found in a response.
/// </summary>
/// <param name="Challenge">The challenge the client data names.</param>
/// <param name="CredentialId">The credential Id.</param>
public sealed record PasskeyClientResponse(byte[] Challenge, byte[] CredentialId);

/// <summary>
///     A credential from a verified registration.
/// </summary>
/// <param name="CredentialId">The credential Id.</param>
/// <param name="PublicKey">The COSE-encoded public key.</param>
/// <param name="SignCount">The signature counter.</param>
/// <param name="AaGuid">The AAGUID of the authenticator model.</param>
/// <param name="Transports">The transports the browser reported.</param>
/// <param name="IsBackupEligible">Whether it may be backed up.</param>
/// <param name="IsBackedUp">Whether it is backed up.</param>
public sealed record PasskeyCredential(
    byte[] CredentialId,
    byte[] PublicKey,
    uint SignCount,
    Guid AaGuid,
    IReadOnlyList<string> Transports,
    bool IsBackupEligible,
    bool IsBackedUp);

/// <summary>
///     The outcome of <see cref="IPasskeyRelyingParty.VerifyRegistrationAsync"/>.
/// </summary>
/// <param name="Credential">The new credential, or null if the response was rejected.</param>
/// <param name="FailureReason">Why the response was rejected, or null. Never contains secrets.</param>
public sealed record PasskeyRegistrationVerification(PasskeyCredential? Credential, string? FailureReason)
{
    /// <summary>
    ///     Whether the response was accepted.
    /// </summary>
    public bool Succeeded => Credential is not null;

    /// <summary>
    ///     Creates the outcome of a rejected response.
    /// </summary>
    /// <param name="reason">Why it was rejected.</param>
    /// <returns>The outcome.</returns>
    public static PasskeyRegistrationVerification Failed(string reason) => new(null, reason);
}

/// <summary>
///     The outcome of <see cref="IPasskeyRelyingParty.VerifyAuthenticationAsync"/>.
/// </summary>
/// <param name="Succeeded">Whether the signature and all checks are valid.</param>
/// <param name="SignCount">The signature counter of the response.</param>
/// <param name="IsBackedUp">Whether the passkey is backed up now.</param>
/// <param name="FailureReason">Why the response was rejected, or null. Never contains secrets.</param>
/// <param name="SignCountDidNotIncrease">
///     Whether it was rejected because the signature counter did not increase, a sign of a cloned authenticator.
/// </param>
public sealed record PasskeyAuthenticationVerification(
    bool Succeeded,
    uint SignCount,
    bool IsBackedUp,
    string? FailureReason,
    bool SignCountDidNotIncrease = false)
{
    /// <summary>
    ///     Creates the outcome of a rejected response.
    /// </summary>
    /// <param name="reason">Why it was rejected.</param>
    /// <param name="signCountDidNotIncrease">Whether the counter did not increase.</param>
    /// <returns>The outcome.</returns>
    public static PasskeyAuthenticationVerification Failed(string reason, bool signCountDidNotIncrease = false)
        => new(false, 0, false, reason, signCountDidNotIncrease);
}
