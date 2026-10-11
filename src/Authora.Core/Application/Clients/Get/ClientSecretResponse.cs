// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Clients.Get;

/// <summary>
///     Represents the metadata of a Client Secret. The Secret itself is never returned.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-6c963f66afa6">Id of the Secret.</param>
/// <param name="CreatedAt">Point in time (UTC) when the Secret was created.</param>
/// <param name="ExpiresAt">Point in time (UTC) when the Secret expires.</param>
/// <param name="RevokedAt">Point in time (UTC) when the Secret was revoked, or null if it is not revoked.</param>
/// <param name="IsActive" example="true">Whether the Secret can currently be used, i.e. it is neither expired nor revoked.</param>
public sealed record ClientSecretResponse(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    bool IsActive);
