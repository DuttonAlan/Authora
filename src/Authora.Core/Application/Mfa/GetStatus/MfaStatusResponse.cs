// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa.GetStatus;

/// <summary>
///     The multi-factor authentication of a User.
/// </summary>
/// <param name="Enabled">Whether the User must enter a code after the password.</param>
/// <param name="AuthenticatorApp">The authenticator app, or null if none is set up (an unfinished setup does not count).</param>
/// <param name="RecoveryCodes">The recovery codes, or null if the User has none.</param>
public sealed record MfaStatusResponse(
    bool Enabled,
    AuthenticatorAppStatusResponse? AuthenticatorApp,
    RecoveryCodesStatusResponse? RecoveryCodes);

/// <summary>
///     The authenticator app of a User.
/// </summary>
/// <param name="EnrolledAt">Point in time (UTC) when the User set it up.</param>
/// <param name="LastUsedAt">Point in time (UTC) when a code of it was last accepted, including the one that confirmed the setup.</param>
public sealed record AuthenticatorAppStatusResponse(DateTime EnrolledAt, DateTime? LastUsedAt);

/// <summary>
///     The recovery codes of a User. The codes themselves are never shown again.
/// </summary>
/// <param name="Remaining">Number of codes that can still be used.</param>
/// <param name="Total">Number of codes in the set.</param>
/// <param name="CreatedAt">Point in time (UTC) when the set was generated.</param>
/// <param name="LastUsedAt">Point in time (UTC) when a code of the set was last used, or null.</param>
public sealed record RecoveryCodesStatusResponse(int Remaining, int Total, DateTime CreatedAt, DateTime? LastUsedAt);
