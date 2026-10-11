// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Clients;

/// <summary>
///     Represents a newly created Client Secret, including its plain-text value.
/// </summary>
/// <remarks>
///     The plain-text Secret is only returned once and cannot be retrieved later.
/// </remarks>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-6c963f66afa6">Id of the Secret, e.g. to revoke it later.</param>
/// <param name="Secret" example="q3Jv0Wc8uYb1nH4kT9sXe2LmPd7aRfG5zVo6iNwB0yE">The plain-text Secret. Store it securely, it is not shown again.</param>
/// <param name="CreatedAt">Point in time (UTC) when the Secret was created.</param>
/// <param name="ExpiresAt">Point in time (UTC) when the Secret expires, or null if it never expires.</param>
public sealed record IssuedClientSecretResponse(Guid Id, string Secret, DateTime CreatedAt, DateTime? ExpiresAt);
