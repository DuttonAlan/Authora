// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit;

namespace Authora.Core.Application.Account.RequestPasswordReset;

/// <summary>
///     A request for a password reset link, waiting in the <see cref="Abstractions.IPasswordResetRequestQueue"/>.
/// </summary>
/// <param name="Email">The address entered on the page.</param>
/// <param name="Actor">The actor of the request that queued it (its IP address), for the audit events.</param>
public sealed record QueuedPasswordResetRequest(string Email, AuditActor Actor);
