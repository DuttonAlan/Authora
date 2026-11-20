// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Consents;

/// <summary>
///     Represents a Scope of a Consent.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
/// <param name="Name" example="orders.read">Name of the Scope as used in the scope parameter.</param>
/// <param name="DisplayName" example="Read your orders">Display Name of the Scope, as shown on the consent screen.</param>
public sealed record ConsentScopeResponse(Guid Id, string Name, string DisplayName);
