// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Paging;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract to read audit events. Events are written through <see cref="IAuditLog"/>.
/// </summary>
public interface IAuditEventRepository
{
    /// <summary>
    ///     Gets one page of the events that match the filter, newest first.
    /// </summary>
    /// <param name="filter">The search criteria.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The events on the page, with the current Email of the acting User and Name of the acting Client.</returns>
    public Task<PagedResponse<AuditEventResponse>> Search(
        AuditEventFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the newest event of a Type, or null if there is none.
    /// </summary>
    /// <param name="type">The exact Type.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The newest event, or null.</returns>
    public Task<AuditEventResponse?> GetLatest(string type, CancellationToken cancellationToken = default);
}
