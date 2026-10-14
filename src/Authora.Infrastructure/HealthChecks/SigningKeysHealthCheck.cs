// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Infrastructure.Security;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Authora.Infrastructure.HealthChecks;

/// <summary>
///     Reports <see cref="HealthStatus.Degraded"/> while changed key files cannot be loaded (US-035). Authora still
///     works with the previous keys, but the rotation the operator started has not happened.
/// </summary>
/// <param name="provider">The provider of the signing keys.</param>
public sealed class SigningKeysHealthCheck(PemSigningKeyProvider provider) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failure = provider.ReloadFailure;

        return Task.FromResult(failure is null
            ? HealthCheckResult.Healthy($"ID Tokens are signed with key {provider.SigningKey.KeyId}.")
            : HealthCheckResult.Degraded($"The changed key files could not be loaded, the previous keys stay in use: {failure}"));
    }
}
