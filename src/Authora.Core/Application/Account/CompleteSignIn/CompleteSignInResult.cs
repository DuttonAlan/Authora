// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.CompleteSignIn;

/// <summary>
///     The outcome of <see cref="CompleteSignInService"/>.
/// </summary>
public enum CompleteSignInStatus
{
    /// <summary>
    ///     The sign-in is complete and a new Session started.
    /// </summary>
    SignedIn,

    /// <summary>
    ///     The step-up is complete; the existing Session got a new token.
    /// </summary>
    SteppedUp,

    /// <summary>
    ///     The response was wrong. The pending sign-in can be tried again until the User is locked out.
    /// </summary>
    WrongResponse,

    /// <summary>
    ///     The User is locked out after too many failed attempts.
    /// </summary>
    LockedOut,

    /// <summary>
    ///     The pending sign-in can no longer be completed: it is unknown, completed, expired, or the User or the
    ///     Session changed. The User must sign in again.
    /// </summary>
    Expired
}

/// <summary>
///     The result of <see cref="CompleteSignInService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="SessionToken">The plain-text token of the new or stepped-up Session for the cookie, or null.</param>
public sealed record CompleteSignInResult(CompleteSignInStatus Status, string? SessionToken = null)
{
    /// <summary>
    ///     The result for a pending sign-in that can no longer be completed.
    /// </summary>
    public static readonly CompleteSignInResult Expired = new(CompleteSignInStatus.Expired);

    /// <summary>
    ///     The result for a wrong response.
    /// </summary>
    public static readonly CompleteSignInResult WrongResponse = new(CompleteSignInStatus.WrongResponse);

    /// <summary>
    ///     The result while the User is locked out.
    /// </summary>
    public static readonly CompleteSignInResult LockedOut = new(CompleteSignInStatus.LockedOut);

    /// <summary>
    ///     Returns a description without the Session token, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"CompleteSignInResult(Status = {Status})";
}
