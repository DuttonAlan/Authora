// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn.Complete;

/// <summary>
///     The outcome of <see cref="CompleteExternalSignInService"/>.
/// </summary>
public enum ExternalSignInStatus
{
    /// <summary>
    ///     The User is signed in with a new Session.
    /// </summary>
    SignedIn,

    /// <summary>
    ///     The User must complete a second factor for a pending sign-in.
    /// </summary>
    Pending,

    /// <summary>
    ///     The account at the provider is not linked to a User, and no User was created for it: sign-up is turned off,
    ///     or the Email is not verified or not in an allowed domain. The reason is in the logs and the audit log only.
    /// </summary>
    NotLinked,

    /// <summary>
    ///     The account at the provider is not linked, but a User has its verified Email. The browser must sign in to
    ///     that User with the User's existing methods to link it, see <see cref="ExternalSignInResult.PendingLink"/>.
    /// </summary>
    LinkRequired,

    /// <summary>
    ///     The account at the provider was linked to the signed-in User.
    /// </summary>
    Linked,

    /// <summary>
    ///     The account at the provider is already linked to the signed-in User.
    /// </summary>
    AlreadyLinked,

    /// <summary>
    ///     The account at the provider is linked to another User, or the signed-in User already has another account at
    ///     the provider linked.
    /// </summary>
    LinkConflict,

    /// <summary>
    ///     The sign-in or link failed, e.g. the flow expired, the provider returned an error or the ID Token is invalid.
    ///     The reason is in the logs and the audit log only.
    /// </summary>
    Failed
}

/// <summary>
///     The result of <see cref="CompleteExternalSignInService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="ProviderName">The Name of the provider, or null if it is unknown.</param>
/// <param name="ReturnUrl">The local URL to continue with, or null if no flow was found.</param>
/// <param name="SessionToken">The plain-text token of the new Session for the Session cookie, or null.</param>
/// <param name="ChallengeToken">The plain-text token of the pending sign-in for its cookie, or null.</param>
/// <param name="MustEnrollSecondFactor">Whether the signed-in User must set up a second factor right away.</param>
/// <param name="PendingLink">For <see cref="ExternalSignInStatus.LinkRequired"/>: the account to link after the sign-in, for its cookie.</param>
public sealed record ExternalSignInResult(
    ExternalSignInStatus Status,
    string? ProviderName = null,
    string? ReturnUrl = null,
    string? SessionToken = null,
    string? ChallengeToken = null,
    bool MustEnrollSecondFactor = false,
    PendingExternalLink? PendingLink = null)
{
    /// <summary>
    ///     Returns a description without the tokens, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"ExternalSignInResult(Status = {Status}, ProviderName = {ProviderName})";
}
