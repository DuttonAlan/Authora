// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     The WebAuthn ceremony a <see cref="Entities.PasskeyChallenge"/> is for. A challenge is only accepted for its own
///     ceremony.
/// </summary>
public enum PasskeyCeremony
{
    /// <summary>
    ///     Registers a new passkey (<c>navigator.credentials.create</c>).
    /// </summary>
    Registration = 0,

    /// <summary>
    ///     Signs in with a registered passkey (<c>navigator.credentials.get</c>).
    /// </summary>
    Authentication = 1
}
