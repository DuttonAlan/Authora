// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.AdminUi;

namespace Authora.Api.Security;

/// <summary>
///     Adds security headers to every response (US-036). Pages that need other values set them themselves, e.g.
///     <see cref="Pages.SecurityHeadersPageFilter"/>; this middleware never overwrites a header that is already set.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item><c>nosniff</c> keeps browsers from running a JSON response as a script or page.</item>
///         <item>
///             A restrictive Content Security Policy by default: API responses are never rendered, and nothing may
///             frame them. The admin interface gets a policy that only allows its own scripts, styles and API calls.
///         </item>
///         <item><c>no-referrer</c> keeps URLs with codes or ids out of the Referer header of other sites.</item>
///     </list>
///     Swagger UI (only in Development) uses inline scripts, so it gets no Content Security Policy.
/// </remarks>
/// <param name="next">The next middleware.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     The policy of all responses that do not set their own.
    /// </summary>
    public const string DefaultContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>
    ///     The policy of the admin interface: only its own files and calls to Authora, no inline scripts or styles,
    ///     so an injected script cannot run and act as the signed-in administrator.
    /// </summary>
    public const string AdminUiContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; "
        + "form-action 'self'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>
    ///     Adds the headers when the response starts.
    /// </summary>
    /// <param name="context">The HttpContext of the request.</param>
    /// <returns>A Task that completes once the request is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            AddHeaders(context);
            return Task.CompletedTask;
        });

        return next(context);
    }

    private static void AddHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;
        var path = context.Request.Path;

        headers.XContentTypeOptions = "nosniff";

        if (!headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = "no-referrer";
        }

        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrEmpty(headers.XFrameOptions))
        {
            headers.XFrameOptions = "DENY";
        }

        if (path.StartsWithSegments(AdminUiOptions.Path, StringComparison.OrdinalIgnoreCase))
        {
            headers.ContentSecurityPolicy = AdminUiContentSecurityPolicy;

            // Keeps other windows, e.g. one opened by a link, from reaching the admin interface through window.opener.
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            // The files have no version in their names, so browsers must check for a new version after an update.
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-cache";
            }
        }
        else if (string.IsNullOrEmpty(headers.ContentSecurityPolicy))
        {
            headers.ContentSecurityPolicy = DefaultContentSecurityPolicy;
        }
    }
}
