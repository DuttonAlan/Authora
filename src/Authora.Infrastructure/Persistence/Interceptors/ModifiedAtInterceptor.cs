// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Authora.Infrastructure.Persistence.Interceptors;

/// <summary>
///     Sets <c>ModifiedAt</c> on every modified entity that has this property, so services do not have to.
/// </summary>
/// <param name="timeProvider">The TimeProvider to get the current time.</param>
public sealed class ModifiedAtInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string ModifiedAtProperty = "ModifiedAt";

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SetModifiedAt(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetModifiedAt(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetModifiedAt(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified
                && entry.Metadata.FindProperty(ModifiedAtProperty) is not null)
            {
                entry.Property(ModifiedAtProperty).CurrentValue = now;
            }
        }
    }
}
