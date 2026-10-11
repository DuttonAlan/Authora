// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account;

/// <summary>
///     Settings of sign-in and Sessions, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class SignInOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:SignIn";

    /// <summary>
    ///     How long a Session may be unused before it ends. Defaults to 2 hours.
    /// </summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    ///     How long a Session lasts at most, regardless of activity. Defaults to 12 hours.
    /// </summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    ///     Number of failed sign-in attempts in a row that lock a User out. Defaults to 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    ///     How long a User is locked out. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
