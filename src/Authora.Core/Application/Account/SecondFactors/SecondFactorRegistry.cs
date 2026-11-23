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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a method is invalid, the password or used by two factors, or a factor's
    ///     <see cref="ISecondFactor.Method"/> is not among its <see cref="ISecondFactor.Methods"/>.
    /// </exception>
    public SecondFactorRegistry(IEnumerable<ISecondFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);

        _factors = [.. factors];

        foreach (var factor in _factors)
        {
            if (!factor.Methods.Contains(factor.Method, StringComparer.Ordinal)
                || factor.Methods.Any(method => !AuthenticationMethods.IsValid(method) || method == AuthenticationMethods.Password))
            {
                throw new InvalidOperationException($"The second factor {factor.GetType().Name} has an invalid method.");
            }
        }

        var methods = _factors.SelectMany(factor => factor.Methods.Distinct(StringComparer.Ordinal)).ToList();

        if (methods.Distinct(StringComparer.Ordinal).Count() != methods.Count)
        {
            throw new InvalidOperationException("Two second factors have the same method.");
        }
    }

    /// <summary>
    ///     All registered factors, whether or not anyone enrolled in them.
    /// </summary>
    public IReadOnlyList<ISecondFactor> All => _factors;

    /// <summary>
    ///     Checks whether a factor was completed: one of its <see cref="ISecondFactor.Methods"/> is among the methods.
    /// </summary>
    /// <param name="factor">The factor.</param>
    /// <param name="completedMethods">The methods completed so far, e.g. those of a Session.</param>
    /// <returns><see langword="true"/> if the factor was completed.</returns>
    public static bool IsCompleted(ISecondFactor factor, IEnumerable<string> completedMethods)
    {
        ArgumentNullException.ThrowIfNull(factor);
        ArgumentNullException.ThrowIfNull(completedMethods);

        return completedMethods.Any(method => factor.Methods.Contains(method, StringComparer.Ordinal));
    }

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
