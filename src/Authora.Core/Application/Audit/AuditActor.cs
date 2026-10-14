// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit;

/// <summary>
///     Who caused an audit event: the User and Client of the current request and its IP address.
/// </summary>
/// <param name="UserId">The signed-in User or the User of the Access Token, or null.</param>
/// <param name="ClientId">The Client of the Access Token, or null.</param>
/// <param name="IpAddress">The IP address of the request, or null outside of a request.</param>
public sealed record AuditActor(Guid? UserId, Guid? ClientId, string? IpAddress)
{
    /// <summary>
    ///     Authora itself, e.g. a background job or the seeding at startup.
    /// </summary>
    public static readonly AuditActor System = new(UserId: null, ClientId: null, IpAddress: null);
}
