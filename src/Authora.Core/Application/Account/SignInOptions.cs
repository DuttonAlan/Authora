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
    ///     Upper limit of <see cref="SessionIdleTimeout"/>.
    /// </summary>
    public static readonly TimeSpan MaxSessionIdleTimeout = TimeSpan.FromHours(24);

    /// <summary>
    ///     Upper limit of <see cref="SessionLifetime"/>.
    /// </summary>
    public static readonly TimeSpan MaxSessionLifetime = TimeSpan.FromDays(7);

    /// <summary>
    ///     Upper limit of <see cref="MaxFailedAttempts"/>.
    /// </summary>
    public const int MaxFailedAttemptsLimit = 100;

    /// <summary>
    ///     Upper limit of <see cref="LockoutDuration"/>. Kept short, because anyone who knows an Email can lock its
    ///     User out.
    /// </summary>
    public static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(1);

    /// <summary>
    ///     How long a Session may be unused before it ends. Defaults to 2 hours, at most
    ///     <see cref="MaxSessionIdleTimeout"/>.
    /// </summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    ///     How long a Session lasts at most, regardless of activity. Defaults to 12 hours, at most
    ///     <see cref="MaxSessionLifetime"/>.
    /// </summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    ///     Number of failed sign-in attempts in a row that lock a User out. Defaults to 5, at most
    ///     <see cref="MaxFailedAttemptsLimit"/>.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    ///     How long a User is locked out. Defaults to 15 minutes, at most <see cref="MaxLockoutDuration"/>.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
