// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;

namespace Authora.Api.Pages;

/// <summary>
///     Adds security headers to the sign-in and consent pages.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <c>frame-ancestors 'none'</c> and <c>X-Frame-Options</c> prevent clickjacking, e.g. a hidden consent
///             page in a frame of another site.
///         </item>
///         <item>
///             No <c>form-action</c> directive: browsers apply it to the redirect after the consent form, which goes
///             to the Redirect URI of the Client.
///         </item>
///         <item><c>no-referrer</c> keeps the authorization request out of the Referer header of other sites.</item>
///         <item><c>no-store</c> keeps pages with a Session or a pending authorization out of caches.</item>
///     </list>
/// </remarks>
internal sealed class SecurityHeadersPageFilter : IAsyncPageFilter
{
    private const string ContentSecurityPolicy =
        "default-src 'none'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'";

    /// <inheritdoc/>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var headers = context.HttpContext.Response.Headers;

        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";

        await next();
    }
}
