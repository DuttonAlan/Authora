// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Account;

/// <summary>
///     Settings of the password reset by email, bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     Password reset needs email delivery (<c>Authora:Email:Host</c>). Without it, the sign-in page offers no reset and
///     administrators cannot send reset links.
/// </remarks>
public sealed class PasswordResetOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:PasswordReset";

    /// <summary>
    ///     Upper limit of <see cref="MaxEmailsPerHour"/>.
    /// </summary>
    public const int MaxEmailsPerHourLimit = 100;

    /// <summary>
    ///     Lower limit of <see cref="LinkLifetime"/>, so the email can state the lifetime in minutes.
    /// </summary>
    public static readonly TimeSpan MinLinkLifetime = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Upper limit of <see cref="LinkLifetime"/>.
    /// </summary>
    public static readonly TimeSpan MaxLinkLifetime = EmailToken.MaxLifetime;

    /// <summary>
    ///     Whether Users can ask for a reset link on the sign-in page ("Forgot password?"). Defaults to
    ///     <see langword="true"/>. Links sent by an administrator work either way.
    /// </summary>
    public bool SelfServiceEnabled { get; set; } = true;

    /// <summary>
    ///     How long a reset link works. Defaults to 30 minutes, between <see cref="MinLinkLifetime"/> and
    ///     <see cref="MaxLinkLifetime"/>.
    /// </summary>
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     How many reset links the sign-in page sends to one address per hour, against flooding a mailbox. Further
    ///     requests get the same answer but send nothing. Defaults to 3.
    /// </summary>
    public int MaxEmailsPerHour { get; set; } = 3;
}
