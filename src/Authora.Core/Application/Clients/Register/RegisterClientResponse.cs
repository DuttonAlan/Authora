// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Clients.Register;

/// <summary>
///     Represents the response for registering a Client.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the registered Client, used as its OAuth client_id.</param>
/// <param name="Secret">The first Secret of a Confidential Client, shown only once. Null for Public Clients.</param>
public sealed record RegisterClientResponse(Guid Id, IssuedClientSecretResponse? Secret);
