// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Mfa;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Mfa.StartEnrollment;

/// <summary>
///     The Service for a signed-in User to start setting up an authenticator app: creates the secret the User adds to
///     the app, which counts as a second factor only once <see cref="ConfirmEnrollment.ConfirmTotpEnrollmentService"/>
///     confirmed it with a first code.
/// </summary>
/// <remarks>
///     A setup that can still be confirmed is shown again instead of replaced, so reloading the page keeps the QR code
///     the User may already have scanned. An expired one gets a new secret. Not recorded in the audit log: until it is
///     confirmed, the secret changes nothing about how the User signs in.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the Email for the app.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to store the unconfirmed authenticator.</param>
/// <param name="secretProtector">Encrypts the secret.</param>
/// <param name="timeProvider">The TimeProvider for the creation time.</param>
/// <param name="options">The settings with the name shown in the app.</param>
/// <param name="logger">The Logger to record the start.</param>
public sealed class StartTotpEnrollmentService(
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider,
    IOptions<MfaOptions> options,
    ILogger<StartTotpEnrollmentService> logger)
{
    /// <summary>
    ///     Starts the setup, or continues one that can still be confirmed.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The secret and the <c>otpauth://</c> URI for the app.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">
    ///     Thrown when the User already has a confirmed authenticator app, or another request started the setup at the
    ///     same time.
    /// </exception>
    public async Task<TotpEnrollmentResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken);

        if (user is null || !user.CanSignIn)
        {
            throw new NotFoundException($"User '{userId}' was not found.");
        }

        // Held until the commit, so a confirmation in another tab sees either the old secret or the new one, never a
        // secret that is replaced after it was checked.
        await using var transaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var authenticator = await mfaRepository.GetTotpAuthenticator(user.Id, cancellationToken);

        if (authenticator is { IsConfirmed: true })
        {
            throw new ConflictException("An authenticator app is already set up. Remove it first.");
        }

        if (authenticator is not null && authenticator.CanBeConfirmed(now)
            && secretProtector.Unprotect(authenticator.ProtectedSecret) is { } pending)
        {
            return CreateResponse(user.Email, pending, authenticator.CreatedAt + TotpAuthenticator.EnrollmentLifetime);
        }

        var secret = Totp.GenerateSecret();
        var protectedSecret = secretProtector.Protect(secret);

        if (authenticator is null)
        {
            authenticator = TotpAuthenticator.Create(user.Id, protectedSecret, now);
            mfaRepository.AddTotpAuthenticator(authenticator);
        }
        else
        {
            authenticator.Restart(protectedSecret, now);
        }

        await mfaRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.TotpEnrollmentStarted(user.Id);

        return CreateResponse(user.Email, secret, now + TotpAuthenticator.EnrollmentLifetime);
    }

    private TotpEnrollmentResponse CreateResponse(string email, byte[] secret, DateTime expiresAt)
    {
        try
        {
            var encoded = Base32.Encode(secret);

            // In groups of four, easier to type into an app that cannot scan the QR code.
            var grouped = string.Join(' ', encoded.Chunk(4).Select(group => new string(group)));

            return new TotpEnrollmentResponse(grouped, Totp.BuildUri(options.Value.IssuerName, email, secret), expiresAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
