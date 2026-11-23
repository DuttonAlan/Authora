// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.BackchannelLogout;

/// <summary>
///     Settings of OpenID Connect Back-Channel Logout 1.0, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class BackchannelLogoutOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:BackchannelLogout";

    /// <summary>
    ///     Smallest allowed <see cref="Timeout"/>.
    /// </summary>
    public static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Largest allowed <see cref="Timeout"/>.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Largest allowed <see cref="MaxAttempts"/>.
    /// </summary>
    public const int MaxMaxAttempts = 50;

    /// <summary>
    ///     Smallest allowed <see cref="MaxAge"/>.
    /// </summary>
    public static readonly TimeSpan MinMaxAge = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Largest allowed <see cref="MaxAge"/>.
    /// </summary>
    public static readonly TimeSpan MaxMaxAge = TimeSpan.FromDays(7);

    /// <summary>
    ///     Smallest allowed <see cref="PollInterval"/>.
    /// </summary>
    public static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Largest allowed <see cref="PollInterval"/>.
    /// </summary>
    public static readonly TimeSpan MaxPollInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Largest allowed <see cref="BatchSize"/>.
    /// </summary>
    public const int MaxBatchSize = 100;

    /// <summary>
    ///     Delay before the first retry. It doubles with every further failure, up to <see cref="MaxRetryDelay"/>.
    /// </summary>
    public static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Longest delay between two attempts.
    /// </summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    /// <summary>
    ///     Whether Back-Channel Logout URIs may use plain HTTP. Defaults to <see langword="false"/>: only HTTPS, because
    ///     the Logout Token identifies the User. Only for development; Authora does not start with it outside the
    ///     environment <c>Development</c>.
    /// </summary>
    public bool AllowHttp { get; set; }

    /// <summary>
    ///     How long a delivery may take before it counts as failed. Defaults to 5 seconds, between
    ///     <see cref="MinTimeout"/> and <see cref="MaxTimeout"/>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How many deliveries of a notification are attempted before Authora gives up. Defaults to 10, between 1 and
    ///     <see cref="MaxMaxAttempts"/>.
    /// </summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    ///     How long after the Session ended a notification is still delivered; afterwards Authora gives up, even with
    ///     attempts left. Defaults to 1 day, between <see cref="MinMaxAge"/> and <see cref="MaxMaxAge"/>.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    ///     How often each instance looks for due notifications. Defaults to 5 seconds, between
    ///     <see cref="MinPollInterval"/> and <see cref="MaxPollInterval"/>.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How many notifications an instance sends at the same time. Defaults to 20, between 1 and
    ///     <see cref="MaxBatchSize"/>.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    ///     How long an instance holds the notifications it claimed. Another instance takes them over afterwards, e.g.
    ///     if the first one stopped while sending.
    /// </summary>
    public TimeSpan ClaimDuration => Timeout + TimeSpan.FromMinutes(1);

    /// <summary>
    ///     The delay before the next attempt after a failure.
    /// </summary>
    /// <param name="attempts">The number of failed attempts so far, including the one that just failed.</param>
    /// <returns><see cref="InitialRetryDelay"/>, doubled for every earlier failure, at most <see cref="MaxRetryDelay"/>.</returns>
    public static TimeSpan GetRetryDelay(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        // 2^12 * 10 seconds is already more than the maximum, so larger exponents are not needed.
        var factor = 1L << Math.Min(attempts - 1, 12);
        var delay = InitialRetryDelay * factor;

        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }
}
