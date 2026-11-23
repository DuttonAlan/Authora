// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     A passkey of a User. The credential and its public key are not shown.
/// </summary>
/// <param name="Id">Id of the passkey.</param>
/// <param name="Name">The name the User gave it.</param>
/// <param name="CreatedAt">Point in time (UTC) when it was registered.</param>
/// <param name="LastUsedAt">Point in time (UTC) when it was last used to sign in, or null.</param>
/// <param name="IsSynced">Whether it may be backed up or synced to other devices; a Session then records <c>swk</c> instead of <c>hwk</c>.</param>
/// <param name="AaGuid">The AAGUID of the authenticator model, all zeros if the authenticator does not reveal it.</param>
/// <param name="Transports">How the browser reaches the authenticator, e.g. <c>usb</c> or <c>internal</c>.</param>
public sealed record PasskeyResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    bool IsSynced,
    Guid AaGuid,
    IReadOnlyList<string> Transports)
{
    /// <summary>
    ///     Creates the response for a passkey.
    /// </summary>
    /// <param name="passkey">The passkey.</param>
    /// <returns>The response.</returns>
    public static PasskeyResponse From(Passkey passkey)
    {
        ArgumentNullException.ThrowIfNull(passkey);

        return new PasskeyResponse(
            passkey.Id,
            passkey.Name,
            passkey.CreatedAt,
            passkey.LastUsedAt,
            passkey.IsBackupEligible,
            passkey.AaGuid,
            passkey.TransportList);
    }
}
