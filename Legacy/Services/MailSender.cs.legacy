using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using MimeKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TopMail.Rest.Models;
using TopMail.Rest.Options;

namespace TopMail.Rest.Services;

public class MailSender
{
    private readonly SmtpOptions _smtp;
    private readonly TrackingOptions _tracking;
    private readonly LogFileWriter _log;
    private readonly AzureAdTokenProvider _tokenProvider;

    public MailSender(IOptions<SmtpOptions> smtp, IOptions<TrackingOptions> tracking, LogFileWriter log, AzureAdTokenProvider tokenProvider)
    {
        _smtp = smtp.Value;
        _tracking = tracking.Value;
        _log = log;
        _tokenProvider = tokenProvider;
    }

    private static bool IsHtml(string? typeBody) => typeBody == "1";

    private static IEnumerable<string> SplitAddresses(string value)
        => value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private MimeMessage BuildMessage(string mittente, string destinatario, string? bcc, string oggetto, string testoMail, bool isHtml)
    {
        var fromAddress = string.IsNullOrWhiteSpace(_smtp.DefaultSender) ? mittente : _smtp.DefaultSender!;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));

        foreach (var to in SplitAddresses(destinatario))
            message.To.Add(MailboxAddress.Parse(to));

        if (!string.IsNullOrWhiteSpace(bcc))
        {
            foreach (var addr in SplitAddresses(bcc))
                message.Bcc.Add(MailboxAddress.Parse(addr));
        }

        message.Subject = oggetto;

        var builder = new BodyBuilder();
        if (isHtml) builder.HtmlBody = testoMail; else builder.TextBody = testoMail;
        message.Body = builder.ToMessageBody();
        return message;
    }

    private void AddAttachments(BodyBuilder builder, List<AttachmentDto>? attachments)
    {
        if (attachments == null) return;
        foreach (var a in attachments)
        {
            var bytes = Convert.FromBase64String(a.Base64);
            builder.Attachments.Add(a.Name, bytes);
        }
    }

    private void AddAttachments(BodyBuilder builder, IFormFileCollection files)
    {
        if (files == null || files.Count == 0) return;
        foreach (var f in files)
        {
            using var ms = new MemoryStream();
            using var input = f.OpenReadStream();
            input.CopyTo(ms);
            builder.Attachments.Add(f.FileName, ms.ToArray());
        }
    }

    private SecureSocketOptions ResolveSecureOptions()
    {
        if (_smtp.Port == 465) return SecureSocketOptions.SslOnConnect;
        return _smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.StartTlsWhenAvailable;
    }

    private void SendWithOAuth(MimeMessage message)
    {
        var token = _tokenProvider.GetAccessTokenAsync().GetAwaiter().GetResult();
        using var client = new SmtpClient();
        client.Connect(_smtp.Host, _smtp.Port, ResolveSecureOptions());
        // Username must be the mailbox principal (USER_MAIL)
        var oauth2 = new SaslMechanismOAuth2(_smtp.Username, token);
        client.Authenticate(oauth2);
        client.Send(message);
        client.Disconnect(true);
    }

    public string HelloWorld() => "Hello World";

    public string InvioMail(BaseMailRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMail), "", "");
        string result;
        try
        {
            var msg = BuildMessage(req.Mittente, req.Destinatario, _tracking.BccAddress, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            SendWithOAuth(msg);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMail), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMail), result, "");
        return result;
    }

    public string InvioMailNoTracking(BaseMailRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMailNoTracking), "", "");
        string result;
        try
        {
            var msg = BuildMessage(req.Mittente, req.Destinatario, null, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            SendWithOAuth(msg);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMailNoTracking), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMailNoTracking), result, "");
        return result;
    }

    public string InvioMailConCCN(CcnMailRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMailConCCN), "", "");
        string result;
        try
        {
            var bcc = string.IsNullOrWhiteSpace(_tracking.BccAddress) ? req.Ccn : string.Join(",", new[]{ _tracking.BccAddress, req.Ccn }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var msg = BuildMessage(req.Mittente, req.Destinatario, bcc, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            SendWithOAuth(msg);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMailConCCN), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMailConCCN), result, "");
        return result;
    }

    public string InvioMailConCCNNoTracking(CcnMailRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMailConCCNNoTracking), "", "");
        string result;
        try
        {
            var msg = BuildMessage(req.Mittente, req.Destinatario, req.Ccn, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            SendWithOAuth(msg);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMailConCCNNoTracking), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMailConCCNNoTracking), result, "");
        return result;
    }

    public string InvioMailWithFiles(MailWithFilesRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMailWithFiles), "", "");
        string result;
        try
        {
            var message = BuildMessage(req.Mittente, req.Destinatario, null, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            var builder = new BodyBuilder();
            if (IsHtml(req.TypeBody)) builder.HtmlBody = req.TestoMail; else builder.TextBody = req.TestoMail;
            AddAttachments(builder, req.Attachments);
            message.Body = builder.ToMessageBody();
            SendWithOAuth(message);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMailWithFiles), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMailWithFiles), result, "");
        return result;
    }

    public string InvioMailWithFilesAndCCN(MailWithFilesAndCcnRequest req)
    {
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "chiamataMetodo", nameof(InvioMailWithFilesAndCCN), "", "");
        string result;
        try
        {
            var message = BuildMessage(req.Mittente, req.Destinatario, req.Ccn, req.Oggetto, req.TestoMail, IsHtml(req.TypeBody));
            var builder = new BodyBuilder();
            if (IsHtml(req.TypeBody)) builder.HtmlBody = req.TestoMail; else builder.TextBody = req.TestoMail;
            AddAttachments(builder, req.Attachments);
            message.Body = builder.ToMessageBody();
            SendWithOAuth(message);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "errore", nameof(InvioMailWithFilesAndCCN), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(req.Mittente, req.Destinatario, req.Oggetto, "fineMetodo", nameof(InvioMailWithFilesAndCCN), result, "");
        return result;
    }

    // Multipart variants (IFormFileCollection)
    public string InvioMailWithFilesMultipart(string mittente, string destinatario, string oggetto, string testoMail, string? typeBody, IFormFileCollection files)
    {
        _log.Write(mittente, destinatario, oggetto, "chiamataMetodo", nameof(InvioMailWithFilesMultipart), "", "");
        string result;
        try
        {
            var message = BuildMessage(mittente, destinatario, null, oggetto, testoMail, IsHtml(typeBody));
            var builder = new BodyBuilder();
            if (IsHtml(typeBody)) builder.HtmlBody = testoMail; else builder.TextBody = testoMail;
            AddAttachments(builder, files);
            message.Body = builder.ToMessageBody();
            SendWithOAuth(message);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(mittente, destinatario, oggetto, "errore", nameof(InvioMailWithFilesMultipart), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(mittente, destinatario, oggetto, "fineMetodo", nameof(InvioMailWithFilesMultipart), result, "");
        return result;
    }

    public string InvioMailWithFilesAndCCNMultipart(string mittente, string destinatario, string ccn, string oggetto, string testoMail, string? typeBody, IFormFileCollection files)
    {
        _log.Write(mittente, destinatario, oggetto, "chiamataMetodo", nameof(InvioMailWithFilesAndCCNMultipart), "", "");
        string result;
        try
        {
            var message = BuildMessage(mittente, destinatario, ccn, oggetto, testoMail, IsHtml(typeBody));
            var builder = new BodyBuilder();
            if (IsHtml(typeBody)) builder.HtmlBody = testoMail; else builder.TextBody = testoMail;
            AddAttachments(builder, files);
            message.Body = builder.ToMessageBody();
            SendWithOAuth(message);
            result = "1";
        }
        catch (Exception ex)
        {
            result = "-1";
            _log.Write(mittente, destinatario, oggetto, "errore", nameof(InvioMailWithFilesAndCCNMultipart), ex.Message, ex.StackTrace ?? "");
        }
        _log.Write(mittente, destinatario, oggetto, "fineMetodo", nameof(InvioMailWithFilesAndCCNMultipart), result, "");
        return result;
    }
}
