// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Account;

/// <summary>
///     Settings of the email verification, bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     Verification links need email delivery (<c>Authora:Email:Host</c>). Without it, no links are sent, and
///     administrators can only mark addresses as verified.
/// </remarks>
public sealed class EmailVerificationOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:EmailVerification";

    /// <summary>
    ///     Lower limit of <see cref="LinkLifetime"/>, so the email can state the lifetime in minutes.
    /// </summary>
    public static readonly TimeSpan MinLinkLifetime = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Upper limit of <see cref="LinkLifetime"/>.
    /// </summary>
    public static readonly TimeSpan MaxLinkLifetime = EmailToken.MaxLifetime;

    /// <summary>
    ///     Whether a verification link is sent by itself when a User is created or their Email is changed. Defaults to
    ///     <see langword="true"/>. Administrators can send links either way.
    /// </summary>
    public bool SendAutomatically { get; set; } = true;

    /// <summary>
    ///     How long a verification link works. Defaults to 24 hours, between <see cref="MinLinkLifetime"/> and
    ///     <see cref="MaxLinkLifetime"/>.
    /// </summary>
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromHours(24);
}
