// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn.CompleteLink;

/// <summary>
///     The outcome of <see cref="CompletePendingLinkService"/>.
/// </summary>
public enum CompletePendingLinkResult
{
    /// <summary>
    ///     The account was linked to the signed-in User.
    /// </summary>
    Linked,

    /// <summary>
    ///     The account was already linked to the signed-in User.
    /// </summary>
    AlreadyLinked,

    /// <summary>
    ///     The browser has not signed in since the account arrived: it must sign in to the User first. The pending link
    ///     is kept.
    /// </summary>
    SignInRequired,

    /// <summary>
    ///     The browser signed in to another User than the one with the Email of the account. Nothing was linked.
    /// </summary>
    WrongUser,

    /// <summary>
    ///     The account is linked to another User, or the User already has another account at the provider linked.
    /// </summary>
    LinkConflict,

    /// <summary>
    ///     There is no pending link, it expired, the provider was disabled or deleted, or the Session ended.
    /// </summary>
    Failed
}
