// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Authora.Infrastructure.Persistence.Interceptors;

/// <summary>
///     Sets <c>ModifiedAt</c> on every changed entity that has this property, so services do not have to.
/// </summary>
/// <remarks>
///     An entity counts as changed if its own row is modified, or if a row it navigates to as a collection is added,
///     modified or deleted. This covers the children of an aggregate (e.g. <c>Client.Secrets</c> and the owned
///     <c>Client.RedirectUris</c>) and its many-to-many links (e.g. <c>User.Roles</c>).
///     Only the side that has the navigation is changed: assigning a Role changes the User, not the Role.
/// </remarks>
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

        // Entries() detects changes first, so collection changes are visible as Added and Deleted entries.
        var entries = context.ChangeTracker.Entries().ToList();

        var entitiesWithModifiedAt = entries
            .Where(entry => entry.Metadata.FindProperty(ModifiedAtProperty) is not null)
            .Where(entry => entry.Metadata.FindPrimaryKey() is { Properties.Count: 1 })
            .ToDictionary(entry => (entry.Metadata, GetPrimaryKey(entry)));

        var changed = new HashSet<EntityEntry>();

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.State == EntityState.Modified && entitiesWithModifiedAt.ContainsValue(entry))
            {
                changed.Add(entry);
            }

            foreach (var foreignKey in entry.Metadata.GetForeignKeys())
            {
                if (!IsCollectionOfPrincipal(foreignKey) || foreignKey.Properties.Count != 1)
                {
                    continue;
                }

                var property = entry.Property(foreignKey.Properties[0].Name);

                // A deleted row still knows its original principal.
                var principalKey = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;

                if (principalKey is not null
                    && entitiesWithModifiedAt.TryGetValue((foreignKey.PrincipalEntityType, principalKey), out var principal)
                    && principal.State is EntityState.Unchanged or EntityState.Modified)
                {
                    changed.Add(principal);
                }
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in changed)
        {
            entry.Property(ModifiedAtProperty).CurrentValue = now;
        }
    }

    /// <summary>
    ///     Checks whether the principal of the foreign key navigates to the dependent rows as part of itself:
    ///     either through a collection navigation (e.g. <c>Client.Secrets</c>, owned <c>Client.RedirectUris</c>)
    ///     or through a many-to-many skip navigation (e.g. <c>User.Roles</c> via <c>user_roles</c>).
    /// </summary>
    /// <remarks>
    ///     EF Core also creates a skip navigation without a CLR member for the side that has no navigation
    ///     (e.g. from Role back to its Users). Such shadow navigations are ignored, so that assigning a Role does
    ///     not change the Role.
    /// </remarks>
    private static bool IsCollectionOfPrincipal(IReadOnlyForeignKey foreignKey)
    {
        return foreignKey.PrincipalToDependent is { } navigation && !navigation.IsShadowProperty()
            || foreignKey.GetReferencingSkipNavigations().Any(skipNavigation => !skipNavigation.IsShadowProperty());
    }

    private static object GetPrimaryKey(EntityEntry entry)
    {
        var keyProperty = entry.Metadata.FindPrimaryKey()!.Properties[0];

        return entry.Property(keyProperty.Name).CurrentValue!;
    }
}
