namespace TopMail.Rest.Services;

public interface IMailBodyFormatter
{
    MailBodyContent Format(string? rawBody, bool isHtml);
}
