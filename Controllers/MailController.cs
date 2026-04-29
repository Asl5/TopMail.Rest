using Microsoft.AspNetCore.Mvc;
using TopMail.Rest.Models;
using TopMail.Rest.Models.Requests;
using TopMail.Rest.Models.Responses;
using TopMail.Rest.Services;

namespace TopMail.Rest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MailController : ControllerBase
{
    private readonly IMailService _mailService;

    public MailController(IMailService mailService)
    {
        _mailService = mailService;
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SendMailResponse>> Send([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        var result = await _mailService.SendAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(result.HttpStatus, result);
    }

    [HttpPost("send-multipart")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SendMailResponse>> SendMultipart(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);

        var request = new SendMailRequest
        {
            Mittente = form["mittente"].ToString(),
            Destinatario = ParseAddresses(form["destinatario"]),
            Cc = ParseAddresses(form["cc"]),
            Ccn = ParseAddresses(form["ccn"]),
            ReplyTo = form["replyTo"].ToString(),
            Oggetto = form["oggetto"].ToString(),
            TestoMail = form["testoMail"].ToString(),
            TypeBody = form["typeBody"].ToString()
        };

        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        var attachments = await ToAttachmentsAsync(form, cancellationToken);
        var result = await _mailService.SendAsync(request, attachments, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(result.HttpStatus, result);
    }

    private static List<string> ParseAddresses(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return new List<string>();

        return rawValue
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static async Task<IReadOnlyCollection<MailAttachment>> ToAttachmentsAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var attachments = new List<MailAttachment>();
        var inlineCids = ParseInlineCids(form["inlineCids"]);
        var inlineIndex = 0;

        foreach (var file in form.Files)
        {
            if (file.Length <= 0)
                continue;

            var isInline = string.Equals(file.Name, "inlineFiles", StringComparison.OrdinalIgnoreCase);
            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            string? cid = null;
            if (isInline)
            {
                cid = inlineIndex < inlineCids.Count ? inlineCids[inlineIndex] : $"inline-{Guid.NewGuid():N}";
                inlineIndex++;
            }

            attachments.Add(new MailAttachment
            {
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                IsInline = isInline,
                ContentId = cid,
                Content = memoryStream.ToArray()
            });
        }

        return attachments;
    }

    private static List<string> ParseInlineCids(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return new List<string>();

        return rawValue
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim('<', '>', ' '))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}
