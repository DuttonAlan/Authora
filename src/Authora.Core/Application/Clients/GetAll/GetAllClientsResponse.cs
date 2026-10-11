// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Clients.GetAll;

/// <summary>
///     Represents a single Client in the response for getting all Clients.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client, used as its OAuth client_id.</param>
/// <param name="Name" example="Shop Web App">Display Name of the Client.</param>
/// <param name="Type" example="Confidential">Type of the Client.</param>
/// <param name="Status" example="Enabled">Status of the Client. Disabled Clients cannot obtain new tokens.</param>
public sealed record GetAllClientsResponse(Guid Id, string Name, ClientType Type, ClientStatus Status);
