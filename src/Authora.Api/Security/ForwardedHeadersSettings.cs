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
    ///     Shortest allowed prefix of an IPv4 network in <see cref="KnownNetworks"/>, the size of <c>10.0.0.0/8</c>.
    /// </summary>
    public const int MinIPv4PrefixLength = 8;

    /// <summary>
    ///     Shortest allowed prefix of an IPv6 network in <see cref="KnownNetworks"/>, the size of the private
    ///     range <c>fc00::/7</c>.
    /// </summary>
    public const int MinIPv6PrefixLength = 7;

    // Length of the prefix ::ffff:0:0/96 of IPv4-mapped IPv6 addresses.
    private const int IPv4MappedPrefixLength = 96;

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

        foreach (var network in KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsed))
            {
                failures.Add($"'{SectionName}:{nameof(KnownNetworks)}' contains '{network}', which is not a network in CIDR notation.");
            }
            else if (IsTooBroad(parsed))
            {
                failures.Add(
                    $"'{SectionName}:{nameof(KnownNetworks)}' contains '{network}', which is broader than "
                    + $"/{MinIPv4PrefixLength} (IPv4) or /{MinIPv6PrefixLength} (IPv6). Trusting it would let almost "
                    + "anyone fake their IP address. List only the networks of the reverse proxies.");
            }
        }

        if (ForwardLimit is < 1 or > 10)
        {
            failures.Add($"'{SectionName}:{nameof(ForwardLimit)}' must be between 1 and 10.");
        }

        return failures;
    }

    /// <summary>
    ///     Whether a network is so broad that trusting it trusts nearly every client, e.g. <c>0.0.0.0/0</c>, <c>::/0</c>,
    ///     or <c>::ffff:0:0/96</c> and <c>::/80</c>, which contain every IPv4 address in its IPv4-mapped form.
    /// </summary>
    private static bool IsTooBroad(System.Net.IPNetwork network)
    {
        if (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return network.PrefixLength < MinIPv4PrefixLength;
        }

        if (network.PrefixLength >= IPv4MappedPrefixLength)
        {
            return network.BaseAddress.IsIPv4MappedToIPv6
                && network.PrefixLength - IPv4MappedPrefixLength < MinIPv4PrefixLength;
        }

        return network.PrefixLength < MinIPv6PrefixLength || ContainsIPv4MappedRange(network);
    }

    /// <summary>
    ///     Whether a network shorter than /96 contains <c>::ffff:0:0/96</c>, i.e. every IPv4-mapped address.
    /// </summary>
    /// <remarks>
    ///     Compares the bits itself, because <see cref="System.Net.IPNetwork.Contains"/> reports every IPv4-mapped
    ///     address as contained in any IPv6 network.
    /// </remarks>
    private static bool ContainsIPv4MappedRange(System.Net.IPNetwork network)
    {
        var networkBytes = network.BaseAddress.GetAddressBytes();
        var mappedBytes = IPAddress.Parse("::ffff:0.0.0.0").GetAddressBytes();

        for (var bit = 0; bit < network.PrefixLength; bit++)
        {
            var mask = 0x80 >> (bit % 8);

            if ((networkBytes[bit / 8] & mask) != (mappedBytes[bit / 8] & mask))
            {
                return false;
            }
        }

        return true;
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
