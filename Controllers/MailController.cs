using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TopMail.Rest.Models;
using TopMail.Rest.Models.Requests;
using TopMail.Rest.Models.Responses;
using TopMail.Rest.Security;
using TopMail.Rest.Services;

namespace TopMail.Rest.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = ApiKeyHmacAuthenticationDefaults.SchemeName)]
public class MailController : ControllerBase
{
    private readonly IMailService _mailService;
    private readonly IClientPolicyEvaluator _clientPolicyEvaluator;
    private readonly ILogger<MailController> _logger;

    public MailController(
        IMailService mailService,
        IClientPolicyEvaluator clientPolicyEvaluator,
        ILogger<MailController> logger)
    {
        _mailService = mailService;
        _clientPolicyEvaluator = clientPolicyEvaluator;
        _logger = logger;
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SendMailResponse>> Send([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        LogRequestStart("send");
        _logger.LogInformation(
            "Mail send request received. ClientId={ClientId}, TypeBody={TypeBody}, ToCount={ToCount}, CcCount={CcCount}, BccCount={BccCount}, BodyLength={BodyLength}.",
            GetClientId(),
            request.TypeBody,
            request.Destinatario.Count,
            request.Cc.Count,
            request.Ccn.Count,
            request.TestoMail?.Length ?? 0);

        if (!_clientPolicyEvaluator.TryValidate(User, request, out var policyError))
        {
            _logger.LogWarning(
                "Mail send request forbidden by client policy. ClientId={ClientId}, Reason={Reason}.",
                GetClientId(),
                policyError);
            return StatusCode(StatusCodes.Status403Forbidden, CreateForbiddenResponse(policyError));
        }

        var result = await _mailService.SendAsync(request, cancellationToken);
        _logger.LogInformation(
            "Mail send completed. ClientId={ClientId}, Success={Success}, HttpStatus={HttpStatus}, Code={Code}, FallbackUsed={FallbackUsed}.",
            GetClientId(),
            result.Success,
            result.HttpStatus,
            result.Code,
            result.FallbackUsed);

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
        LogRequestStart("send-multipart");
        var form = await Request.ReadFormAsync(cancellationToken);
        _logger.LogInformation(
            "Multipart mail request received. ClientId={ClientId}, FileCount={FileCount}, InlineCidCount={InlineCidCount}, TypeBody={TypeBody}, BodyLength={BodyLength}.",
            GetClientId(),
            form.Files.Count,
            ParseInlineCids(form["inlineCids"]).Count,
            form["typeBody"].ToString(),
            form["testoMail"].ToString().Length);

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
            _logger.LogWarning(
                "Multipart mail request model validation failed. ClientId={ClientId}.",
                GetClientId());
            return ValidationProblem(ModelState);
        }

        if (!_clientPolicyEvaluator.TryValidate(User, request, out var policyError))
        {
            _logger.LogWarning(
                "Multipart mail request forbidden by client policy. ClientId={ClientId}, Reason={Reason}.",
                GetClientId(),
                policyError);
            return StatusCode(StatusCodes.Status403Forbidden, CreateForbiddenResponse(policyError));
        }

        var attachments = await ToAttachmentsAsync(form, cancellationToken);
        _logger.LogInformation(
            "Multipart attachments normalized. ClientId={ClientId}, AttachmentCount={AttachmentCount}.",
            GetClientId(),
            attachments.Count);

        var result = await _mailService.SendAsync(request, attachments, cancellationToken);
        _logger.LogInformation(
            "Multipart mail send completed. ClientId={ClientId}, Success={Success}, HttpStatus={HttpStatus}, Code={Code}, FallbackUsed={FallbackUsed}.",
            GetClientId(),
            result.Success,
            result.HttpStatus,
            result.Code,
            result.FallbackUsed);

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

    private static SendMailResponse CreateForbiddenResponse(string message)
    {
        return new SendMailResponse
        {
            HttpStatus = StatusCodes.Status403Forbidden,
            Success = false,
            Code = "FORBIDDEN",
            Message = message,
            FallbackUsed = false
        };
    }

    private string GetClientId()
    {
        return User.FindFirst("client_id")?.Value ?? "anonymous";
    }

    private void LogRequestStart(string operationName)
    {
        _logger.LogInformation("================================================================");
        _logger.LogInformation(
            "BEGIN MAIL REQUEST {Operation}. TraceId={TraceId}, ClientId={ClientId}, Method={Method}, Path={Path}.",
            operationName,
            HttpContext.TraceIdentifier,
            GetClientId(),
            HttpContext.Request.Method,
            HttpContext.Request.Path.Value);
        _logger.LogInformation("================================================================");
    }
}
