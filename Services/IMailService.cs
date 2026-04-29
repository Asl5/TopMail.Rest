using TopMail.Rest.Models;
using TopMail.Rest.Models.Requests;
using TopMail.Rest.Models.Responses;

namespace TopMail.Rest.Services;

public interface IMailService
{
    Task<SendMailResponse> SendAsync(SendMailRequest request, CancellationToken cancellationToken);
    Task<SendMailResponse> SendAsync(SendMailRequest request, IReadOnlyCollection<MailAttachment> attachments, CancellationToken cancellationToken);
}
