// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa.ConfirmEnrollment;

/// <summary>
///     The outcome of <see cref="ConfirmTotpEnrollmentService"/>.
/// </summary>
public enum ConfirmTotpEnrollmentStatus
{
    /// <summary>
    ///     The authenticator app is set up.
    /// </summary>
    Confirmed,

    /// <summary>
    ///     The code is wrong; the User can try again.
    /// </summary>
    WrongCode,

    /// <summary>
    ///     There is no setup to confirm, or it expired. The User starts again.
    /// </summary>
    Expired,

    /// <summary>
    ///     An authenticator app is already set up.
    /// </summary>
    AlreadyEnrolled
}

/// <summary>
///     The result of <see cref="ConfirmTotpEnrollmentService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="RecoveryCodes">The new recovery codes in plain text, once confirmed; shown to the User only once.</param>
public sealed record ConfirmTotpEnrollmentResult(ConfirmTotpEnrollmentStatus Status, IReadOnlyList<string> RecoveryCodes)
{
    /// <summary>
    ///     The result for a wrong code.
    /// </summary>
    public static readonly ConfirmTotpEnrollmentResult WrongCode = new(ConfirmTotpEnrollmentStatus.WrongCode, []);

    /// <summary>
    ///     The result when there is nothing to confirm.
    /// </summary>
    public static readonly ConfirmTotpEnrollmentResult Expired = new(ConfirmTotpEnrollmentStatus.Expired, []);

    /// <summary>
    ///     The result when an authenticator app is already set up.
    /// </summary>
    public static readonly ConfirmTotpEnrollmentResult AlreadyEnrolled = new(ConfirmTotpEnrollmentStatus.AlreadyEnrolled, []);

    /// <summary>
    ///     Returns a description without the recovery codes, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"ConfirmTotpEnrollmentResult(Status = {Status})";
}
