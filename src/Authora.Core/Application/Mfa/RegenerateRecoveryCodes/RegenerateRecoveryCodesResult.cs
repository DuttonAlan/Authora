// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa.RegenerateRecoveryCodes;

/// <summary>
///     The outcome of <see cref="RegenerateRecoveryCodesService"/>.
/// </summary>
public enum RegenerateRecoveryCodesStatus
{
    /// <summary>
    ///     The new codes replace the previous ones.
    /// </summary>
    Regenerated,

    /// <summary>
    ///     The User has no authenticator app, which the codes stand in for.
    /// </summary>
    NotEnrolled,

    /// <summary>
    ///     The Session was not authenticated with the authenticator app; the User completes a step-up first.
    /// </summary>
    StepUpRequired,

    /// <summary>
    ///     The Session ended in the meantime; the User must sign in again.
    /// </summary>
    SessionEnded
}

/// <summary>
///     The result of <see cref="RegenerateRecoveryCodesService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="RecoveryCodes">The new codes in plain text, once regenerated; shown to the User only once.</param>
public sealed record RegenerateRecoveryCodesResult(RegenerateRecoveryCodesStatus Status, IReadOnlyList<string> RecoveryCodes)
{
    /// <summary>
    ///     The result when the User has no authenticator app.
    /// </summary>
    public static readonly RegenerateRecoveryCodesResult NotEnrolled = new(RegenerateRecoveryCodesStatus.NotEnrolled, []);

    /// <summary>
    ///     The result when the Session must complete a step-up first.
    /// </summary>
    public static readonly RegenerateRecoveryCodesResult StepUpRequired = new(RegenerateRecoveryCodesStatus.StepUpRequired, []);

    /// <summary>
    ///     The result when the Session ended.
    /// </summary>
    public static readonly RegenerateRecoveryCodesResult SessionEnded = new(RegenerateRecoveryCodesStatus.SessionEnded, []);

    /// <summary>
    ///     Returns a description without the recovery codes, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"RegenerateRecoveryCodesResult(Status = {Status})";
}
