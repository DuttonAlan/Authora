// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     A database transaction. Disposing it without <see cref="CommitAsync"/> rolls it back.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    ///     Commits the transaction and releases the locks it holds.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the transaction is committed.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default);
}
