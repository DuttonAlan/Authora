// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Email;

/// <summary>
///     An email to send, with a plain-text and an HTML version of the same content. Create it with
///     <see cref="EmailTemplates"/>.
/// </summary>
/// <param name="ToAddress">The address of the recipient.</param>
/// <param name="ToName">The display name of the recipient, or null.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="TextBody">The plain-text body.</param>
/// <param name="HtmlBody">The HTML body. Every value in it must be HTML encoded.</param>
public sealed record EmailMessage(string ToAddress, string? ToName, string Subject, string TextBody, string HtmlBody);
