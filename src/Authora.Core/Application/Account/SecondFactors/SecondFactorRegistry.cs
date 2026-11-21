// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;

namespace Authora.Core.Application.Account.SecondFactors;

/// <summary>
///     The registered <see cref="ISecondFactor"/>s, and which of them a User enrolled in.
/// </summary>
public sealed class SecondFactorRegistry
{
    private readonly IReadOnlyList<ISecondFactor> _factors;

    /// <summary>
    ///     Creates the registry.
    /// </summary>
    /// <param name="factors">The registered factors.</param>
    /// <exception cref="InvalidOperationException">Thrown when a method is invalid, the password or used twice.</exception>
    public SecondFactorRegistry(IEnumerable<ISecondFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);

        _factors = [.. factors];

        foreach (var factor in _factors)
        {
            if (!AuthenticationMethods.IsValid(factor.Method) || factor.Method == AuthenticationMethods.Password)
            {
                throw new InvalidOperationException($"The second factor {factor.GetType().Name} has an invalid method.");
            }
        }

        if (_factors.DistinctBy(factor => factor.Method, StringComparer.Ordinal).Count() != _factors.Count)
        {
            throw new InvalidOperationException("Two second factors have the same method.");
        }
    }

    /// <summary>
    ///     All registered factors, whether or not anyone enrolled in them.
    /// </summary>
    public IReadOnlyList<ISecondFactor> All => _factors;

    /// <summary>
    ///     Gets the factors the User enrolled in.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The enrolled factors, in the order they were registered.</returns>
    public async Task<IReadOnlyList<ISecondFactor>> GetEnrolledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var enrolled = new List<ISecondFactor>();

        foreach (var factor in _factors)
        {
            if (await factor.IsEnrolledAsync(userId, cancellationToken))
            {
                enrolled.Add(factor);
            }
        }

        return enrolled;
    }
}
