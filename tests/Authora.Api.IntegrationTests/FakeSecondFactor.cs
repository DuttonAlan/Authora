// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Collections.Concurrent;

using Authora.Core.Application.Account.SecondFactors;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     A second factor for the tests: a User is enrolled with a fixed code, next to the authenticator app, to test the sign-in with any factor and with several.
/// </summary>
public sealed class FakeSecondFactor : ISecondFactor
{
    /// <summary>
    ///     The method the fake records, a smart card (RFC 8176), so it does not collide with the <c>otp</c> of the authenticator app nor the <c>hwk</c> and <c>swk</c> of passkeys.
    /// </summary>
    public const string MethodName = "sc";

    private readonly ConcurrentDictionary<Guid, string> _codes = new();

    private readonly ConcurrentDictionary<Guid, Func<Task>> _whileVerifying = new();

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

    /// <summary>
    ///     Runs an action while the response of a User is verified, e.g. to let time pass in a slow verification.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="action">The action, run once per verification.</param>
    public void WhileVerifying(Guid userId, Func<Task> action)
    {
        _whileVerifying[userId] = action;
    }

    /// <inheritdoc/>
    public Task<bool> IsEnrolledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_codes.ContainsKey(userId));
    }

    /// <inheritdoc/>
    public async Task<string?> VerifyAsync(Guid userId, string response, CancellationToken cancellationToken = default)
    {
        if (_whileVerifying.TryGetValue(userId, out var action))
        {
            await action();
        }

        return _codes.TryGetValue(userId, out var code) && code == response ? MethodName : null;
    }
}
