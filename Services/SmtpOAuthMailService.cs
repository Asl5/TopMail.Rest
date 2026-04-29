using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using Microsoft.Extensions.Options;
using MimeKit;
using TopMail.Rest.Models;
using TopMail.Rest.Models.Requests;
using TopMail.Rest.Models.Responses;
using TopMail.Rest.Options;

namespace TopMail.Rest.Services;

public class SmtpOAuthMailService : IMailService
{
    private readonly SmtpOptions _smtp;
    private readonly TrackingOptions _tracking;
    private readonly AttachmentSecurityOptions _attachmentSecurity;
    private readonly AzureAdTokenProvider _tokenProvider;
    private readonly IMailBodyFormatter _mailBodyFormatter;
    private readonly ILogger<SmtpOAuthMailService> _logger;

    public SmtpOAuthMailService(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<TrackingOptions> trackingOptions,
        IOptions<AttachmentSecurityOptions> attachmentSecurityOptions,
        AzureAdTokenProvider tokenProvider,
        IMailBodyFormatter mailBodyFormatter,
        ILogger<SmtpOAuthMailService> logger)
    {
        _smtp = smtpOptions.Value;
        _tracking = trackingOptions.Value;
        _attachmentSecurity = attachmentSecurityOptions.Value;
        _tokenProvider = tokenProvider;
        _mailBodyFormatter = mailBodyFormatter;
        _logger = logger;
    }

    public async Task<SendMailResponse> SendAsync(SendMailRequest request, CancellationToken cancellationToken)
    {
        return await SendAsync(request, Array.Empty<MailAttachment>(), cancellationToken);
    }

    public async Task<SendMailResponse> SendAsync(SendMailRequest request, IReadOnlyCollection<MailAttachment> attachments, CancellationToken cancellationToken)
    {
        var requestedFrom = ResolveFromAddress(request.Mittente);
        if (!IsValidEmailAddress(requestedFrom))
        {
            return CreateClientError($"Mittente non valido: '{requestedFrom}'.", requestedFrom);
        }

        try
        {
            var validationError = ValidateConfiguration();
            if (validationError is not null)
            {
                return CreateServerError(validationError, requestedFrom);
            }

            var attachmentError = ValidateAttachments(attachments);
            if (attachmentError is not null)
            {
                return CreateClientError(attachmentError, requestedFrom);
            }

            var recipientSet = BuildRecipients(request);
            if (recipientSet.Error is not null)
            {
                return CreateClientError(recipientSet.Error, requestedFrom);
            }

            var message = BuildMessage(request, requestedFrom, attachments, recipientSet);
            await SendMessageAsync(message, _smtp.Username, cancellationToken);

            return new SendMailResponse
            {
                HttpStatus = StatusCodes.Status200OK,
                Success = true,
                Code = "1",
                Message = "Mail inviata correttamente.",
                FallbackUsed = false,
                RequestedFrom = requestedFrom,
                EffectiveFrom = requestedFrom
            };
        }
        catch (SmtpCommandException ex) when (CanFallbackToDefaultSender(ex, requestedFrom))
        {
            _logger.LogWarning(
                "Invio con mittente '{RequestedFrom}' non consentito. Fallback completo su '{FallbackFrom}'.",
                requestedFrom,
                _smtp.Username);

            try
            {
                var fallbackMessage = BuildMessage(request, _smtp.Username, attachments, recipientSet: null);
                await SendMessageAsync(fallbackMessage, _smtp.Username, cancellationToken);
                return new SendMailResponse
                {
                    HttpStatus = StatusCodes.Status200OK,
                    Success = true,
                    Code = "1",
                    Message = $"Mittente '{requestedFrom}' non disponibile. Mail inviata con fallback '{_smtp.Username}'.",
                    FallbackUsed = true,
                    InitialError = ex.Message,
                    RequestedFrom = requestedFrom,
                    EffectiveFrom = _smtp.Username
                };
            }
            catch (Exception finalFallbackEx)
            {
                _logger.LogError(finalFallbackEx, "Errore durante l'invio fallback con mittente SMTP principale.");
                return new SendMailResponse
                {
                    HttpStatus = StatusCodes.Status500InternalServerError,
                    Success = false,
                    Code = "-1",
                    Message = finalFallbackEx.Message,
                    FallbackUsed = true,
                    InitialError = ex.Message,
                    RequestedFrom = requestedFrom,
                    EffectiveFrom = _smtp.Username
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'invio della mail SMTP OAuth2.");
            return new SendMailResponse
            {
                HttpStatus = StatusCodes.Status500InternalServerError,
                Success = false,
                Code = "-1",
                Message = ex.Message,
                FallbackUsed = false,
                RequestedFrom = requestedFrom,
                EffectiveFrom = requestedFrom
            };
        }
    }

    private string? ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host))
            return "Configurazione SMTP mancante: Smtp:Host.";

        if (_smtp.Port <= 0)
            return "Configurazione SMTP non valida: Smtp:Port.";

        if (string.IsNullOrWhiteSpace(_smtp.Username))
            return "Configurazione SMTP mancante: Smtp:Username.";

        if (!string.IsNullOrWhiteSpace(_tracking.BccAddress) &&
            !System.Net.Mail.MailAddress.TryCreate(_tracking.BccAddress, out _))
            return "Configurazione non valida: Tracking:BccAddress.";

        return null;
    }

    private async Task SendMessageAsync(MimeMessage message, string smtpAuthUser, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, ResolveSecureOptions(), cancellationToken);
        await client.AuthenticateAsync(new SaslMechanismOAuth2(smtpAuthUser, token), cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private MimeMessage BuildMessage(SendMailRequest request, string fromAddress, IReadOnlyCollection<MailAttachment> attachments, RecipientSet? recipientSet)
    {
        var recipients = recipientSet ?? BuildRecipients(request);
        if (recipients.Error is not null)
        {
            throw new InvalidOperationException(recipients.Error);
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));

        AddMailboxes(message.To, recipients.To);
        AddMailboxes(message.Cc, recipients.Cc);
        AddMailboxes(message.Bcc, recipients.Bcc);
        AddMailboxes(message.ReplyTo, recipients.ReplyTo);

        message.Subject = request.Oggetto;

        var bodyBuilder = new BodyBuilder();
        var body = _mailBodyFormatter.Format(request.TestoMail, request.TypeBody == "1");

        if (!string.IsNullOrWhiteSpace(body.HtmlBody))
        {
            bodyBuilder.HtmlBody = body.HtmlBody;
        }
        if (!string.IsNullOrWhiteSpace(body.TextBody))
        {
            bodyBuilder.TextBody = body.TextBody;
        }
        AddAttachments(bodyBuilder, attachments);

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private string ResolveFromAddress(string? requestedSender)
    {
        if (!string.IsNullOrWhiteSpace(requestedSender))
            return requestedSender.Trim();

        return _smtp.Username;
    }

    private bool CanFallbackToDefaultSender(SmtpCommandException exception, string requestedFrom)
    {
        if (requestedFrom.Equals(_smtp.Username, StringComparison.OrdinalIgnoreCase))
            return false;

        return exception.Message.Contains("SendAsDenied", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("not allowed to send as", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("Authentication unsuccessful", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("5.7.3", StringComparison.OrdinalIgnoreCase);
    }

    private string? ValidateAttachments(IReadOnlyCollection<MailAttachment> attachments)
    {
        if (attachments.Count == 0)
            return null;

        if (attachments.Count > _attachmentSecurity.MaxAttachmentCount)
        {
            return $"Numero allegati superiore al massimo consentito ({_attachmentSecurity.MaxAttachmentCount}).";
        }

        long totalBytes = 0;
        foreach (var attachment in attachments)
        {
            totalBytes += attachment.Length;

            if (attachment.Length <= 0)
                return $"Allegato '{attachment.FileName}' non valido: file vuoto.";

            if (attachment.Length > _attachmentSecurity.MaxAttachmentSizeBytes)
            {
                return $"Allegato '{attachment.FileName}' supera il limite di {_attachmentSecurity.MaxAttachmentSizeBytes} byte.";
            }

            var extension = Path.GetExtension(attachment.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (_attachmentSecurity.BlockedExtensions.Any(x => extension.Equals(x, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Allegato '{attachment.FileName}' non consentito per motivi di sicurezza.";
            }

            if (_attachmentSecurity.AllowedExtensions.Length > 0 &&
                !_attachmentSecurity.AllowedExtensions.Any(x => extension.Equals(x, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Estensione non consentita per allegato '{attachment.FileName}'.";
            }

            if (attachment.IsInline &&
                !attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return $"Inline image '{attachment.FileName}' non valida: solo content-type image/* consentito.";
            }
        }

        if (totalBytes > _attachmentSecurity.MaxTotalAttachmentSizeBytes)
        {
            return $"Dimensione totale allegati superiore al massimo consentito ({_attachmentSecurity.MaxTotalAttachmentSizeBytes} byte).";
        }

        return null;
    }

    private static void AddAttachments(BodyBuilder bodyBuilder, IReadOnlyCollection<MailAttachment> attachments)
    {
        foreach (var attachment in attachments)
        {
            var safeName = SanitizeFileName(attachment.FileName);
            if (attachment.IsInline)
            {
                var linked = bodyBuilder.LinkedResources.Add(safeName, attachment.Content, ContentType.Parse(attachment.ContentType));
                linked.ContentId = SanitizeContentId(attachment.ContentId);
                linked.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            }
            else
            {
                bodyBuilder.Attachments.Add(safeName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }
    }

    private static string SanitizeFileName(string? fileName)
    {
        var candidate = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = "attachment.bin";

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalid, '_');
        }

        return candidate;
    }

    private static string SanitizeContentId(string? contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return $"inline-{Guid.NewGuid():N}";

        var clean = contentId.Trim('<', '>', ' ');
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            clean = clean.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(clean) ? $"inline-{Guid.NewGuid():N}" : clean;
    }

    private static bool IsValidEmailAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return System.Net.Mail.MailAddress.TryCreate(email, out _);
    }

    private RecipientSet BuildRecipients(SendMailRequest request)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var toResult = NormalizeAddresses(request.Destinatario, seen, "destinatario");
        if (toResult.Error is not null) return RecipientSet.WithError(toResult.Error);

        var ccResult = NormalizeAddresses(request.Cc, seen, "cc");
        if (ccResult.Error is not null) return RecipientSet.WithError(ccResult.Error);

        var bccResult = NormalizeAddresses(request.Ccn, seen, "ccn");
        if (bccResult.Error is not null) return RecipientSet.WithError(bccResult.Error);

        if (!string.IsNullOrWhiteSpace(_tracking.BccAddress) &&
            System.Net.Mail.MailAddress.TryCreate(_tracking.BccAddress, out var parsedTrackingBcc))
        {
            if (seen.Add(parsedTrackingBcc.Address))
            {
                bccResult.Valid.Add(parsedTrackingBcc.Address);
            }
        }

        var replyTo = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            if (!System.Net.Mail.MailAddress.TryCreate(request.ReplyTo.Trim(), out var parsedReply))
            {
                return RecipientSet.WithError($"Indirizzo replyTo non valido: '{request.ReplyTo}'.");
            }
            replyTo.Add(parsedReply.Address);
        }

        if (toResult.Valid.Count == 0)
        {
            return RecipientSet.WithError("Almeno un destinatario valido e' obbligatorio.");
        }

        return new RecipientSet
        {
            To = toResult.Valid,
            Cc = ccResult.Valid,
            Bcc = bccResult.Valid,
            ReplyTo = replyTo
        };
    }

    private static AddressNormalizationResult NormalizeAddresses(IEnumerable<string> rawAddresses, HashSet<string> seen, string fieldName)
    {
        var normalized = new List<string>();
        foreach (var item in rawAddresses)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            var parts = item.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!System.Net.Mail.MailAddress.TryCreate(part, out var parsed))
                {
                    return AddressNormalizationResult.WithError($"Indirizzo non valido in '{fieldName}': '{part}'.");
                }

                var canonical = parsed.Address;
                if (seen.Add(canonical))
                {
                    normalized.Add(canonical);
                }
            }
        }

        return AddressNormalizationResult.WithSuccess(normalized);
    }

    private SendMailResponse CreateClientError(string message, string? requestedFrom)
    {
        return new SendMailResponse
        {
            HttpStatus = StatusCodes.Status400BadRequest,
            Success = false,
            Code = "VALIDATION_ERROR",
            Message = message,
            FallbackUsed = false,
            RequestedFrom = requestedFrom,
            EffectiveFrom = requestedFrom
        };
    }

    private SendMailResponse CreateServerError(string message, string? requestedFrom)
    {
        return new SendMailResponse
        {
            HttpStatus = StatusCodes.Status500InternalServerError,
            Success = false,
            Code = "SERVER_ERROR",
            Message = message,
            FallbackUsed = false,
            RequestedFrom = requestedFrom,
            EffectiveFrom = requestedFrom
        };
    }

    private static void AddMailboxes(InternetAddressList target, IEnumerable<string> rawAddresses)
    {
        foreach (var raw in rawAddresses)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var addresses = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var address in addresses)
            {
                target.Add(MailboxAddress.Parse(address));
            }
        }
    }

    private SecureSocketOptions ResolveSecureOptions()
    {
        if (_smtp.Port == 465)
            return SecureSocketOptions.SslOnConnect;

        return _smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.StartTlsWhenAvailable;
    }

    private sealed class RecipientSet
    {
        public List<string> To { get; init; } = new();
        public List<string> Cc { get; init; } = new();
        public List<string> Bcc { get; init; } = new();
        public List<string> ReplyTo { get; init; } = new();
        public string? Error { get; init; }

        public static RecipientSet WithError(string error) => new() { Error = error };
    }

    private sealed class AddressNormalizationResult
    {
        public List<string> Valid { get; init; } = new();
        public string? Error { get; init; }

        public static AddressNormalizationResult WithSuccess(List<string> valid) => new() { Valid = valid };
        public static AddressNormalizationResult WithError(string error) => new() { Error = error };
    }
}
