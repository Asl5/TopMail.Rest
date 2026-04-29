using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.Extensions.Options;
using TopMail.Rest.Options;

namespace TopMail.Rest.Services;

public class MailBodyFormatter : IMailBodyFormatter
{
    private readonly MailBodyOptions _options;
    private readonly HtmlSanitizer _sanitizer;

    public MailBodyFormatter(IOptions<MailBodyOptions> options)
    {
        _options = options.Value;
        _sanitizer = BuildSanitizer();
    }

    public MailBodyContent Format(string? rawBody, bool isHtml)
    {
        var content = Normalize(rawBody);

        if (!isHtml)
        {
            return new MailBodyContent
            {
                TextBody = content
            };
        }

        var html = _options.SanitizeHtml ? _sanitizer.Sanitize(content) : content;
        if (_options.WrapHtmlDocument)
        {
            html = WrapHtml(html);
        }

        string? textAlternative = null;
        if (_options.GenerateTextAlternative)
        {
            textAlternative = ConvertHtmlToText(html);
        }

        return new MailBodyContent
        {
            HtmlBody = html,
            TextBody = textAlternative
        };
    }

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // Common tags/attributes used in email templates.
        sanitizer.AllowedTags.Add("table");
        sanitizer.AllowedTags.Add("thead");
        sanitizer.AllowedTags.Add("tbody");
        sanitizer.AllowedTags.Add("tfoot");
        sanitizer.AllowedTags.Add("tr");
        sanitizer.AllowedTags.Add("th");
        sanitizer.AllowedTags.Add("td");
        sanitizer.AllowedTags.Add("hr");
        sanitizer.AllowedTags.Add("img");
        sanitizer.AllowedAttributes.Add("style");
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedAttributes.Add("width");
        sanitizer.AllowedAttributes.Add("height");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string WrapHtml(string html)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"></head><body>");
        sb.Append(html);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string ConvertHtmlToText(string html)
    {
        var withoutTags = Regex.Replace(html, "<[^>]*>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
