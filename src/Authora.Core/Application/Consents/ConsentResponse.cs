// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Consents;

/// <summary>
///     Represents the Consent of a User for a Client.
/// </summary>
/// <param name="ClientId" example="0199d1a2-7c3e-7b4a-9f10-1c963f66afa6">Id of the Client.</param>
/// <param name="ClientName" example="Shop Web App">Name of the Client.</param>
/// <param name="Scopes">The approved Scopes, ordered by their Name.</param>
/// <param name="CreatedAt" example="2026-11-02T18:22:00Z">When the User first approved the Client (UTC).</param>
/// <param name="ModifiedAt" example="2026-11-05T09:12:00Z">When Scopes were last added (UTC), or null if they never were.</param>
public sealed record ConsentResponse(
    Guid ClientId,
    string ClientName,
    IReadOnlyList<ConsentScopeResponse> Scopes,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
