// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.ExternalSignIn;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders;

/// <summary>
///     Fetches the discovery document of an Issuer once before a provider is saved with it, so a typo is reported to
///     the administrator instead of to the first User who signs in.
/// </summary>
/// <param name="relyingParty">Fetches and checks the discovery document.</param>
/// <param name="logger">The Logger to record rejected Issuers.</param>
public sealed class IssuerValidator(IOidcRelyingParty relyingParty, ILogger<IssuerValidator> logger)
{
    /// <summary>
    ///     Checks that the Issuer publishes a usable discovery document.
    /// </summary>
    /// <param name="issuer">The Issuer.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Issuer is verified.</returns>
    /// <exception cref="DomainValidationException">Thrown when the discovery document cannot be fetched or is unusable.</exception>
    public async Task EnsureValidAsync(string issuer, CancellationToken cancellationToken = default)
    {
        try
        {
            await relyingParty.ValidateIssuerAsync(issuer, cancellationToken);
        }
        catch (ExternalSignInException exception)
        {
            logger.IssuerRejected(issuer, exception.Message);

            throw new DomainValidationException(
                $"The discovery document of the Issuer cannot be used: {exception.Message}",
                nameof(issuer));
        }
    }
}
