// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text;

using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.ClientAuthentication;

using Microsoft.Net.Http.Headers;

namespace Authora.Api.OAuth;

/// <summary>
///     Reads the form body and the Client credentials of a request to the token, revocation or introspection endpoint.
/// </summary>
internal sealed class OAuthForm
{
    private const string FormContentType = "application/x-www-form-urlencoded";

    private readonly IFormCollection _form;
    private readonly HttpRequest _request;

    private OAuthForm(HttpRequest request, IFormCollection form)
    {
        _request = request;
        _form = form;
    }

    /// <summary>
    ///     Reads the form body of the request.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The form.</returns>
    /// <exception cref="OAuthException">Thrown when the body is not <c>application/x-www-form-urlencoded</c>.</exception>
    public static async Task<OAuthForm> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !contentType.MediaType.Equals(FormContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, $"The request body must be {FormContentType}.");
        }

        return new OAuthForm(request, await request.ReadFormAsync(cancellationToken));
    }

    /// <summary>
    ///     Gets a parameter. Parameters without a value are treated as omitted (RFC 6749, Section 3.1).
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The value, or null if it was not sent.</returns>
    /// <exception cref="OAuthException">Thrown when the parameter was sent more than once.</exception>
    public string? Get(string name)
    {
        var values = _form[name];

        if (values.Count > 1)
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, $"The parameter {name} must not be sent more than once.");
        }

        return string.IsNullOrEmpty(values) ? null : values.ToString();
    }

    /// <summary>
    ///     Gets the Client credentials from the Authorization header (<c>client_secret_basic</c>) or from the body
    ///     (<c>client_secret_post</c>, or only <c>client_id</c> for Public Clients).
    /// </summary>
    /// <returns>The credentials.</returns>
    /// <exception cref="OAuthException">
    ///     Thrown when the Authorization header is malformed, or the Client uses more than one authentication method.
    /// </exception>
    public ClientCredentials GetClientCredentials()
    {
        var bodyClientId = Get("client_id");
        var bodyClientSecret = Get("client_secret");

        if (!AuthenticationHeaderValueParser.TryGetBasicCredentials(_request, out var basic))
        {
            return new ClientCredentials(bodyClientId, bodyClientSecret);
        }

        // RFC 6749, Section 2.3: the Client must not use more than one authentication method.
        if (bodyClientSecret is not null)
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The Client must use only one authentication method.");
        }

        if (bodyClientId is not null && bodyClientId != basic.ClientId)
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The client_id in the body does not match the Authorization header.");
        }

        return basic;
    }

    /// <summary>
    ///     Parses the <c>Basic</c> scheme of the Authorization header (RFC 6749, Section 2.3.1).
    /// </summary>
    private static class AuthenticationHeaderValueParser
    {
        public static bool TryGetBasicCredentials(HttpRequest request, out ClientCredentials credentials)
        {
            credentials = null!;

            var header = request.Headers.Authorization.ToString();

            if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string decoded;

            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            }
            catch (FormatException)
            {
                throw InvalidHeader();
            }

            var separator = decoded.IndexOf(':', StringComparison.Ordinal);

            if (separator <= 0)
            {
                throw InvalidHeader();
            }

            // The client_id and secret are form-encoded before they are joined (RFC 6749, Section 2.3.1).
            var secret = FormDecode(decoded[(separator + 1)..]);

            credentials = new ClientCredentials(
                FormDecode(decoded[..separator]),
                secret.Length == 0 ? null : secret);

            return true;
        }

        private static OAuthException InvalidHeader()
        {
            return new OAuthException(OAuthErrors.InvalidClient, "The Authorization header is malformed.");
        }

        private static string FormDecode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
