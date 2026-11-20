// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Net;
using System.Text;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Email;

/// <summary>
///     The emails Authora sends, each as plain text and HTML with the same content.
/// </summary>
/// <remarks>
///     Every value in the HTML version is HTML encoded, so a name like <c>&lt;b&gt;</c> is shown as typed. Links must
///     be absolute HTTP(S) URLs, e.g. built from the Issuer.
/// </remarks>
public static class EmailTemplates
{
    /// <summary>
    ///     The email with a link to choose a new password.
    /// </summary>
    /// <param name="user">The User who asked for the reset.</param>
    /// <param name="link">The link that contains the one-time token.</param>
    /// <param name="validFor">How long the link works.</param>
    /// <returns>The email to send to the User.</returns>
    /// <exception cref="ArgumentException">Thrown when the link is not an absolute HTTP(S) URL.</exception>
    public static EmailMessage PasswordReset(User user, Uri link, TimeSpan validFor)
    {
        return Build(
            user,
            link,
            subject: "Reset your password",
            intro: $"someone asked to reset the password of your account {user?.Email}. To choose a new password, open "
                + $"this link within {FormatDuration(validFor)}:",
            buttonText: "Choose a new password",
            outro: "If you did not ask for this, ignore this email. Your password stays unchanged.");
    }

    /// <summary>
    ///     The email with a link to confirm the User's address.
    /// </summary>
    /// <param name="user">The User whose address is confirmed, with that address.</param>
    /// <param name="link">The link that contains the one-time token.</param>
    /// <param name="validFor">How long the link works.</param>
    /// <returns>The email to send to the User.</returns>
    /// <exception cref="ArgumentException">Thrown when the link is not an absolute HTTP(S) URL.</exception>
    public static EmailMessage EmailVerification(User user, Uri link, TimeSpan validFor)
    {
        return Build(
            user,
            link,
            subject: "Confirm your email address",
            intro: $"please confirm that {user?.Email} is your email address by opening this link within "
                + $"{FormatDuration(validFor)}:",
            buttonText: "Confirm email address",
            outro: "If you did not create an account or change its email address, ignore this email.");
    }

    /// <summary>
    ///     The email that tells a User that their password was changed, so they notice if someone else did it.
    /// </summary>
    /// <param name="user">The User whose password was changed.</param>
    /// <param name="changedAt">When the password was changed (UTC).</param>
    /// <returns>The email to send to the User.</returns>
    public static EmailMessage PasswordChanged(User user, DateTime changedAt)
    {
        return Build(
            user,
            link: null,
            subject: "Your password was changed",
            intro: $"the password of your account {user?.Email} was changed on "
                + $"{changedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC. You were signed out "
                + "everywhere else, and applications will ask you to sign in again.",
            buttonText: null,
            outro: "If you did not change your password, contact your administrator right away.");
    }

    /// <summary>
    ///     Formats a duration for an email, e.g. <c>30 minutes</c> or <c>24 hours</c>.
    /// </summary>
    /// <param name="duration">The duration, at least one minute.</param>
    /// <returns>The duration in whole hours if it is a multiple of an hour, otherwise in minutes.</returns>
    public static string FormatDuration(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.FromMinutes(1));

        if (duration.Ticks % TimeSpan.TicksPerHour == 0)
        {
            var hours = (long)duration.TotalHours;

            return string.Create(CultureInfo.InvariantCulture, $"{hours} {(hours == 1 ? "hour" : "hours")}");
        }

        var minutes = (long)duration.TotalMinutes;

        return string.Create(CultureInfo.InvariantCulture, $"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
    }

    // Without a link, the email only informs and has no button.
    private static EmailMessage Build(User? user, Uri? link, string subject, string intro, string? buttonText, string outro)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (link is not null
            && (!link.IsAbsoluteUri || (link.Scheme != Uri.UriSchemeHttps && link.Scheme != Uri.UriSchemeHttp)))
        {
            throw new ArgumentException("The link must be an absolute HTTP(S) URL.", nameof(link));
        }

        var greeting = $"Hello {user.FirstName},";
        var url = link?.AbsoluteUri;

        var text = new StringBuilder()
            .Append(greeting).Append("\r\n\r\n")
            .Append(intro).Append("\r\n\r\n");

        if (url is not null)
        {
            text.Append(url).Append("\r\n\r\n");
        }

        text.Append(outro).Append("\r\n");

        var html = new StringBuilder()
            .Append("<!DOCTYPE html>\r\n")
            .Append("<html lang=\"en\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n")
            .Append("<title>").Append(Encode(subject)).Append("</title>\r\n</head>\r\n")
            .Append("<body style=\"margin:0;padding:24px;background:#f5f5f7;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1d1d1f;\">\r\n")
            .Append("<div style=\"max-width:560px;margin:0 auto;padding:32px;background:#ffffff;border-radius:8px;line-height:1.5;\">\r\n")
            .Append("<p>").Append(Encode(greeting)).Append("</p>\r\n")
            .Append("<p>").Append(Encode(intro)).Append("</p>\r\n");

        if (url is not null)
        {
            html.Append("<p><a href=\"").Append(Encode(url))
                .Append("\" style=\"display:inline-block;padding:10px 20px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;\">")
                .Append(Encode(buttonText ?? url)).Append("</a></p>\r\n")
                .Append("<p style=\"font-size:13px;color:#6e6e73;word-break:break-all;\">").Append(Encode(url)).Append("</p>\r\n");
        }

        html.Append("<p>").Append(Encode(outro)).Append("</p>\r\n")
            .Append("</div>\r\n</body>\r\n</html>\r\n");

        return new EmailMessage(user.Email, $"{user.FirstName} {user.LastName}", subject, text.ToString(), html.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
