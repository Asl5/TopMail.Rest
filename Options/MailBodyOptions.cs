namespace TopMail.Rest.Options;

public class MailBodyOptions
{
    public bool SanitizeHtml { get; set; } = true;
    public bool GenerateTextAlternative { get; set; } = true;
    public bool WrapHtmlDocument { get; set; } = true;
}
