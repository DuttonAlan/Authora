// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;
using Authora.Core.Domain.Entities;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the emails: both versions contain the link, and values are HTML encoded.
/// </summary>
public sealed class EmailTemplateTests
{
    private static readonly Uri Link = new("https://auth.example.com/account/reset-password?token=abc&x=1");

    public static TheoryData<string> Templates => [nameof(EmailTemplates.PasswordReset), nameof(EmailTemplates.EmailVerification)];

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_ContainsTheLinkInBothVersions(string template)
    {
        var user = User.Create("ada@example.com", "Ada", "Lovelace", "hash");

        var message = Render(template, user, TimeSpan.FromMinutes(30));

        Assert.Equal("ada@example.com", message.ToAddress);
        Assert.Equal("Ada Lovelace", message.ToName);
        Assert.Contains(Link.AbsoluteUri, message.TextBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://auth.example.com/account/reset-password?token=abc&amp;x=1\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("30 minutes", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("30 minutes", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_EncodesValuesInHtml(string template)
    {
        var user = User.Create("ada@example.com", "<script>alert(1)</script>", "Lovelace", "hash");

        var message = Render(template, user, TimeSpan.FromHours(1));

        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("1 hour", message.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:ada@example.com")]
    public void Template_RejectsLinksThatAreNoHttpUrls(string link)
    {
        var user = User.Create("ada@example.com", "Ada", "Lovelace", "hash");

        Assert.Throws<ArgumentException>(() => EmailTemplates.PasswordReset(user, new Uri(link), TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData(1, "1 minute")]
    [InlineData(15, "15 minutes")]
    [InlineData(90, "90 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(1440, "24 hours")]
    public void FormatDuration_UsesHoursOnlyForWholeHours(int minutes, string expected)
    {
        Assert.Equal(expected, EmailTemplates.FormatDuration(TimeSpan.FromMinutes(minutes)));
    }

    private static EmailMessage Render(string template, User user, TimeSpan validFor) => template switch
    {
        nameof(EmailTemplates.PasswordReset) => EmailTemplates.PasswordReset(user, Link, validFor),
        _ => EmailTemplates.EmailVerification(user, Link, validFor)
    };
}
