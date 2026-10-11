// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Authora.Core.Domain.Entities.User>;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Hashes passwords with the ASP.NET Core Identity password hasher: PBKDF2 with HMAC-SHA512, a random salt and a
///     versioned format, without the rest of ASP.NET Core Identity.
/// </summary>
/// <remarks>
///     Hashes with fewer iterations than <see cref="IterationCount"/> still verify, but report
///     <see cref="PasswordVerification.SuccessRehashNeeded"/>, so they are upgraded on the next sign-in.
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    ///     Number of PBKDF2-HMAC-SHA512 iterations, as recommended by OWASP (Password Storage Cheat Sheet).
    /// </summary>
    public const int IterationCount = 210_000;

    private readonly IdentityPasswordHasher _hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = IterationCount
    }));

    /// <inheritdoc/>
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        // The Identity hasher does not use the User, so no instance is passed.
        return _hasher.HashPassword(null!, password);
    }

    /// <inheritdoc/>
    public PasswordVerification Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        return _hasher.VerifyHashedPassword(null!, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed
        };
    }
}
