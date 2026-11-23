// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.Pages;

/// <summary>
///     Marks a page that loads the static JavaScript modules of <c>wwwroot/js</c>, e.g. for the WebAuthn calls of
///     passkeys. <see cref="SecurityHeadersPageFilter"/> then allows scripts and requests to the own origin
///     (<c>script-src 'self'; connect-src 'self'</c>); inline scripts stay forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowScriptsAttribute : Attribute
{
}
