// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.ExternalSignIn;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;

namespace Authora.Infrastructure.ExternalSignIn;

/// <summary>
///     Fetches discovery documents and key sets of identity providers with the HTTP client
///     <see cref="OidcRelyingParty.HttpClientName"/>, and only over HTTPS unless
///     <see cref="ExternalSignInOptions.AllowHttp"/> is on.
/// </summary>
/// <param name="httpClientFactory">Creates the HTTP client for every request, so its handler is rotated.</param>
/// <param name="options">The settings with <see cref="ExternalSignInOptions.AllowHttp"/>.</param>
internal sealed class HttpClientDocumentRetriever(IHttpClientFactory httpClientFactory, IOptions<ExternalSignInOptions> options)
    : IDocumentRetriever
{
    /// <summary>
    ///     Largest document accepted; discovery documents and key sets are a few kilobytes.
    /// </summary>
    public const int MaxDocumentLength = 512 * 1024;

    /// <inheritdoc/>
    public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        if (!OidcRelyingParty.IsAllowedUrl(address, options.Value))
        {
            throw new ExternalSignInException($"The URL '{address}' of the provider does not use HTTPS.");
        }

        using var http = httpClientFactory.CreateClient(OidcRelyingParty.HttpClientName);
        using var response = await http.GetAsync(new Uri(address), HttpCompletionOption.ResponseHeadersRead, cancel);

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalSignInException($"'{address}' returned {(int)response.StatusCode}.");
        }

        if (response.Content.Headers.ContentLength > MaxDocumentLength)
        {
            throw new ExternalSignInException($"'{address}' returned a document larger than {MaxDocumentLength} bytes.");
        }

        var document = await response.Content.ReadAsStringAsync(cancel);

        return document.Length > MaxDocumentLength
            ? throw new ExternalSignInException($"'{address}' returned a document larger than {MaxDocumentLength} bytes.")
            : document;
    }
}
