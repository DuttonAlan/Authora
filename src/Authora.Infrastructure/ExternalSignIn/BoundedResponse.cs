// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text;

using Authora.Core.Application.ExternalSignIn;

namespace Authora.Infrastructure.ExternalSignIn;

/// <summary>
///     Reads the bodies of responses of identity providers with a limit on their size, so a slow or endless response
///     can neither run past the deadline nor fill the memory.
/// </summary>
/// <remarks>
///     <see cref="HttpClient.Timeout"/> only covers the response headers when the body is read as a stream, so callers
///     pass a token that is cancelled at the deadline of the whole request, see <see cref="CreateDeadline"/>.
/// </remarks>
internal static class BoundedResponse
{
    private const int BufferSize = 8 * 1024;

    /// <summary>
    ///     Creates a token source that is cancelled with the caller's token or once the timeout passed.
    /// </summary>
    /// <param name="timeout">The time the whole request, including its body, may take.</param>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <returns>The token source; dispose it after the request.</returns>
    public static CancellationTokenSource CreateDeadline(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);

        return source;
    }

    /// <summary>
    ///     Reads the body as UTF-8 text, at most <paramref name="maxBytes"/> bytes.
    /// </summary>
    /// <param name="content">The body.</param>
    /// <param name="maxBytes">The largest accepted size.</param>
    /// <param name="cancellationToken">A token that is cancelled at the deadline.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ExternalSignInException">Thrown when the body is larger than <paramref name="maxBytes"/>.</exception>
    public static async Task<string> ReadAsStringAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Headers.ContentLength > maxBytes)
        {
            throw new ExternalSignInException($"The response is larger than {maxBytes} bytes.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[BufferSize];
        int read;

        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new ExternalSignInException($"The response is larger than {maxBytes} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
