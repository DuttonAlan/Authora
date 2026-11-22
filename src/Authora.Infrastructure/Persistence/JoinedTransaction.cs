// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     Stands for the transaction of the caller that a lock joined; its owner commits it.
/// </summary>
internal sealed class JoinedTransaction : ITransaction
{
    /// <summary>
    ///     The only instance; it holds no state.
    /// </summary>
    public static readonly JoinedTransaction Instance = new();

    private JoinedTransaction()
    {
    }

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
