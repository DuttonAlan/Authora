// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Application.Audit.Search;

/// <summary>
///     The Service to search the audit log.
/// </summary>
/// <param name="auditEventRepository">An Instance of a IAuditEventRepository to search the events.</param>
public sealed class SearchAuditEventsService(IAuditEventRepository auditEventRepository)
{
    /// <summary>
    ///     Gets one page of the events that match the filter, newest first.
    /// </summary>
    /// <param name="filter">The search criteria.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The events on the page and the total number of matching events.</returns>
    /// <exception cref="DomainValidationException">Thrown when <c>From</c> is not before <c>To</c>.</exception>
    public async Task<PagedResponse<AuditEventResponse>> ExecuteAsync(
        AuditEventFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);

        if (filter.From >= filter.To)
        {
            throw new DomainValidationException("'from' must be before 'to'.", nameof(filter));
        }

        return await auditEventRepository.Search(filter, page, cancellationToken);
    }
}
