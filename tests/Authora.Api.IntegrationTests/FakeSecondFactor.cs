// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Collections.Concurrent;

using Authora.Core.Application.Account.SecondFactors;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     A second factor for the tests: a User is enrolled with a fixed code, until the real factors exist.
/// </summary>
public sealed class FakeSecondFactor : ISecondFactor
{
    /// <summary>
    ///     The method the fake records, the one of one-time codes (RFC 8176).
    /// </summary>
    public const string MethodName = "otp";

    private readonly ConcurrentDictionary<Guid, string> _codes = new();

    /// <inheritdoc/>
    public string Method => MethodName;

    /// <inheritdoc/>
    public string DisplayName => "Test authenticator";

    /// <summary>
    ///     Enrolls a User, who then must enter the code after the password.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="code">The code the factor accepts for the User.</param>
    public void Enroll(Guid userId, string code)
    {
        _codes[userId] = code;
    }

    /// <inheritdoc/>
    public Task<bool> IsEnrolledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_codes.ContainsKey(userId));
    }

    /// <inheritdoc/>
    public Task<bool> VerifyAsync(Guid userId, string response, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_codes.TryGetValue(userId, out var code) && code == response);
    }
}
