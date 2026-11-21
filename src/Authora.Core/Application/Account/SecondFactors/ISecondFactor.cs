// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.SecondFactors;

/// <summary>
///     A second factor a User can enroll and must then complete after the password, e.g. an authenticator app.
/// </summary>
/// <remarks>
///     <para>
///         Every registered implementation takes part in the sign-in: once a User is enrolled in one, the password
///         alone no longer starts a Session (see <see cref="SignIn.SignInService"/>). Without any enrolled factor,
///         the sign-in works with the password only.
///     </para>
///     <para>
///         A wrong response counts towards the lockout like a wrong password. Implementations only check the response;
///         they must not record audit events of their own for failures, which the sign-in does.
///     </para>
/// </remarks>
public interface ISecondFactor
{
    /// <summary>
    ///     The Authentication Method Reference value (RFC 8176) the Session records once the factor is completed,
    ///     e.g. <c>otp</c>. Distinct among the registered factors.
    /// </summary>
    public string Method { get; }

    /// <summary>
    ///     The name shown to the User, e.g. "Authenticator app".
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    ///     Checks whether the User enrolled in the factor.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the User can and must complete the factor to sign in.</returns>
    public Task<bool> IsEnrolledAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks the response of the User, e.g. a code.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="response">The response the User entered.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the response is correct.</returns>
    public Task<bool> VerifyAsync(Guid userId, string response, CancellationToken cancellationToken = default);
}
