// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Net;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Receives the Logout Tokens the Api sends to Back-Channel Logout URIs instead of the network.
/// </summary>
public sealed class FakeBackchannelLogoutHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<ReceivedLogoutRequest> _received = new();

    private readonly ConcurrentDictionary<string, HttpStatusCode> _statusCodes = new(StringComparer.Ordinal);

    /// <summary>
    ///     How long every request takes, e.g. to let several deliveries overlap.
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    ///     All requests received so far.
    /// </summary>
    public IReadOnlyList<ReceivedLogoutRequest> Received => [.. _received];

    /// <summary>
    ///     The requests received for a URI.
    /// </summary>
    /// <param name="uri">The Back-Channel Logout URI.</param>
    /// <returns>The requests, in the order they were received.</returns>
    public IReadOnlyList<ReceivedLogoutRequest> ReceivedFor(string uri)
        => [.. _received.Where(request => request.Uri == uri)];

    /// <summary>
    ///     Answers requests to a URI with a status code instead of 200 OK.
    /// </summary>
    /// <param name="uri">The Back-Channel Logout URI.</param>
    /// <param name="statusCode">The status code of the responses.</param>
    public void Respond(string uri, HttpStatusCode statusCode) => _statusCodes[uri] = statusCode;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.RequestUri!.ToString();
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1].Replace('+', ' ') : string.Empty));

        _received.Enqueue(new ReceivedLogoutRequest(
            uri,
            request.Method,
            request.Content?.Headers.ContentType?.MediaType,
            form.GetValueOrDefault("logout_token")));

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        return new HttpResponseMessage(_statusCodes.GetValueOrDefault(uri, HttpStatusCode.OK));
    }
}

/// <summary>
///     A request the Api sent to a Back-Channel Logout URI.
/// </summary>
/// <param name="Uri">The URI.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="MediaType">The media type of the body.</param>
/// <param name="LogoutToken">The <c>logout_token</c> parameter of the body, or null.</param>
public sealed record ReceivedLogoutRequest(string Uri, HttpMethod Method, string? MediaType, string? LogoutToken);
