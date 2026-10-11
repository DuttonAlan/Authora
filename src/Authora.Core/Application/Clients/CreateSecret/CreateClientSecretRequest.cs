// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Clients.CreateSecret;

/// <summary>
///     Represents the request to create a Client Secret.
/// </summary>
/// <param name="ExpiresAt" example="2027-10-11T00:00:00Z">Point in time when the Secret expires, or null for the default lifetime of 365 days. Must be in the future and at most 730 days from now.</param>
public sealed record CreateClientSecretRequest(DateTimeOffset? ExpiresAt);
