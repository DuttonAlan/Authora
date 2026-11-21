// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents the remembered decision of a User to let a Client access the approved Scopes. There is at most one
///     Consent per User and Client.
/// </summary>
/// <remarks>
///     <para>
///         The User is not asked again while a request asks only for Scopes the Consent contains. Approving a request
///         with further Scopes adds them, so the Consent grows with what the User approved.
///     </para>
///     <para>
///         A Consent is not a <see cref="Grant"/>: a Grant is a family of tokens that ends, while the Consent stays
///         until the User or an administrator revokes it. Revoking it also revokes the Grants of the User and Client.
///         A deleted Scope disappears from all Consents, so a new Scope with the same Name is asked for again.
///     </para>
/// </remarks>
public sealed class Consent
{
    private readonly List<Scope> _scopes = [];

    /// <summary>
    ///     Used by EF Core to materialize a Consent from the database.
    /// </summary>
    private Consent()
    {
    }

    /// <summary>
    ///     Id of the Consent.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="User"/> who approved.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="Entities.Client"/> the User approved.
    /// </summary>
    public Guid ClientId { get; private set; }

    /// <summary>
    ///     The Client, only loaded where the Consent is listed, otherwise <see langword="null"/>.
    /// </summary>
    public Client? Client { get; private set; }

    /// <summary>
    ///     The approved Scopes.
    /// </summary>
    public IReadOnlyCollection<Scope> Scopes => _scopes;

    /// <summary>
    ///     Point in time (UTC) when the User first approved the Client.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when Scopes were last added, or <see langword="null"/> if they never were.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Creates the Consent of a User for a Client.
    /// </summary>
    /// <param name="userId">The Id of the User who approved.</param>
    /// <param name="client">The approved Client.</param>
    /// <param name="scopes">The approved Scopes, at least one.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The created Consent.</returns>
    /// <exception cref="ArgumentException">Thrown when no Scope is given or <paramref name="now"/> is not in UTC.</exception>
    public static Consent Create(Guid userId, Client client, IEnumerable<Scope> scopes, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);
        UtcGuard.EnsureUtc(now);

        var consent = new Consent
        {
            UserId = userId,
            ClientId = client.Id,
            CreatedAt = now
        };

        if (consent.AddScopes(scopes).Count == 0)
        {
            throw new ArgumentException("At least one Scope is required.", nameof(scopes));
        }

        return consent;
    }

    /// <summary>
    ///     Adds the Scopes that the Consent does not contain yet.
    /// </summary>
    /// <param name="scopes">The approved Scopes.</param>
    /// <returns>The Names of the added Scopes, empty if the Consent already contained all of them.</returns>
    public IReadOnlyList<string> AddScopes(IEnumerable<Scope> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var added = new List<string>();

        foreach (var scope in scopes)
        {
            if (!_scopes.Any(existing => existing.Id == scope.Id))
            {
                _scopes.Add(scope);
                added.Add(scope.Name);
            }
        }

        return added;
    }

    /// <summary>
    ///     Checks whether the Consent contains all of the given Scopes, so the User need not be asked for them.
    /// </summary>
    /// <param name="scopeNames">The Names of the requested Scopes.</param>
    /// <returns><see langword="true"/> if every Scope is contained.</returns>
    public bool Covers(IEnumerable<string> scopeNames)
    {
        ArgumentNullException.ThrowIfNull(scopeNames);

        return scopeNames.All(name => _scopes.Any(scope => string.Equals(scope.Name, name, StringComparison.Ordinal)));
    }
}
