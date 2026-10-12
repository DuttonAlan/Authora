// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.EntityFrameworkCore.Storage;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     An <see cref="ITransaction"/> backed by an EF Core transaction.
/// </summary>
/// <param name="transaction">The EF Core transaction.</param>
internal sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
{
    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
