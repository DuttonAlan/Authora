// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users.Update;

/// <summary>
///     The Service to update an existing User.
/// </summary>
/// <remarks>
///     A new Email is no longer verified, and a verification link is sent to it in the background if
///     <see cref="EmailVerificationSender.ShouldSendAutomatically"/> allows it.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to update a User.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="emailVerificationSender">Sends a link to confirm a new Email.</param>
/// <param name="auditLog">The audit log to record the change.</param>
public sealed class UpdateUserService(
    IUserRepository userRepository,
    PrivilegeGuard privilegeGuard,
    EmailVerificationSender emailVerificationSender,
    IAuditLog auditLog)
{
    /// <summary>
    ///     Updates an existing User.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted, or the new Email is already used by another User.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is empty, only whitespace, too long, or the Email is not valid.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        User user;
        EmailMessage? verification = null;

        await using (var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken))
        {
            user = await userRepository.GetUserById(id, cancellationToken)
                ?? throw new NotFoundException($"User '{id}' was not found.");

            DeletedUserGuard.EnsureNotDeleted(user);

            string?[] fields =
            [
                request.Email is null ? null : "Email",
                request.FirstName is null ? null : "First Name",
                request.LastName is null ? null : "Last Name"
            ];
            var sentFields = fields.OfType<string>().ToList();

            await privilegeGuard.EnsureCanActOnUserAsync(
                user.Id,
                AuditEntry.ForUser(AuditEventTypes.Users.Updated, user.Id, FormatDetails(sentFields, emailChanged: false)),
                cancellationToken);

            var emailChanged = false;

            if (request.Email is not null)
            {
                var normalizedEmail = User.Normalize(request.Email);

                if (normalizedEmail != user.NormalizedEmail)
                {
                    if (await userRepository.EmailExists(normalizedEmail, cancellationToken))
                    {
                        throw new ConflictException("The Email is already used by another User.");
                    }

                    emailChanged = true;
                }

                // Resets the verification if the address is a different one.
                user.SetEmail(request.Email);
            }

            if (request.FirstName is not null)
            {
                user.SetFirstName(request.FirstName);
            }

            if (request.LastName is not null)
            {
                user.SetLastName(request.LastName);
            }

            auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Users.Updated, user.Id, FormatDetails(sentFields, emailChanged)));

            // Only changed values are written; ModifiedAt is set when saving.
            await userRepository.SaveChangesAsync(cancellationToken);

            // Joins the transaction, so the link is only issued if the change is committed.
            if (emailChanged && emailVerificationSender.ShouldSendAutomatically(user))
            {
                verification = await emailVerificationSender.PrepareAsync(user, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (verification is not null)
        {
            await emailVerificationSender.QueueAsync(user, verification, "the Email was changed", cancellationToken);
        }
    }

    private static string? FormatDetails(List<string> sentFields, bool emailChanged)
    {
        if (sentFields.Count == 0)
        {
            return null;
        }

        var details = $"Fields: {string.Join(", ", sentFields)}";

        return emailChanged ? $"{details}. The new Email is not verified" : details;
    }
}
