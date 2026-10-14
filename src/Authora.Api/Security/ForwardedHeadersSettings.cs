// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace Authora.Api.Security;

/// <summary>
///     Settings for running behind a reverse proxy (US-036), bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are only read from the configured proxies. Otherwise
///     anyone could pretend to come from another IP address, and so bypass the rate limits and fake the IP address
///     in the audit log. Without any proxy configured, the headers are ignored.
/// </remarks>
public sealed class ForwardedHeadersSettings
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:ForwardedHeaders";

    /// <summary>
    ///     IP addresses of the reverse proxies, e.g. <c>10.0.0.5</c>.
    /// </summary>
    public IList<string> KnownProxies { get; set; } = [];

    /// <summary>
    ///     Networks of the reverse proxies in CIDR notation, e.g. <c>10.0.0.0/24</c>.
    /// </summary>
    public IList<string> KnownNetworks { get; set; } = [];

    /// <summary>
    ///     How many proxies are in front of Authora, i.e. how many entries of <c>X-Forwarded-For</c> are processed.
    ///     Defaults to 1, at most 10.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    ///     Whether any proxy is configured.
    /// </summary>
    public bool IsEnabled => KnownProxies.Count > 0 || KnownNetworks.Count > 0;

    /// <summary>
    ///     Validates the settings.
    /// </summary>
    /// <returns>The error messages, empty if the settings are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();

        foreach (var proxy in KnownProxies.Where(proxy => !IPAddress.TryParse(proxy, out _)))
        {
            failures.Add($"'{SectionName}:{nameof(KnownProxies)}' contains '{proxy}', which is not an IP address.");
        }

        foreach (var network in KnownNetworks.Where(network => !System.Net.IPNetwork.TryParse(network, out _)))
        {
            failures.Add($"'{SectionName}:{nameof(KnownNetworks)}' contains '{network}', which is not a network in CIDR notation.");
        }

        if (ForwardLimit is < 1 or > 10)
        {
            failures.Add($"'{SectionName}:{nameof(ForwardLimit)}' must be between 1 and 10.");
        }

        return failures;
    }

    /// <summary>
    ///     Applies the settings to the options of the forwarded headers middleware.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    public void Apply(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ForwardLimit;

        // The defaults trust only the loopback addresses. Replace them with exactly the configured proxies.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in KnownProxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in KnownNetworks)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
