// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
public sealed class UserRepository : IUserRepository
{
    /// <inheritdoc/>
    public Task<Guid> CreateUser(User user, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task DeleteUser(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<User>> GetUsers(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task UpdateUser(User user, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
