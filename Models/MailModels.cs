using System.Text.Json.Serialization;

namespace TopMail.Rest.Models;

public class BaseMailRequest
{
    [JsonPropertyName("mittente")] public string Mittente { get; set; } = string.Empty;
    [JsonPropertyName("destinatario")] public string Destinatario { get; set; } = string.Empty;
    [JsonPropertyName("oggetto")] public string Oggetto { get; set; } = string.Empty;
    [JsonPropertyName("testoMail")] public string TestoMail { get; set; } = string.Empty;
    [JsonPropertyName("typeBody")] public string? TypeBody { get; set; }
}

public class CcnMailRequest : BaseMailRequest
{
    [JsonPropertyName("ccn")] public string Ccn { get; set; } = string.Empty;
}

public class AttachmentDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    // Base64 of the file content
    [JsonPropertyName("base64")] public string Base64 { get; set; } = string.Empty;
}

public class MailWithFilesRequest : BaseMailRequest
{
    [JsonPropertyName("attachments")] public List<AttachmentDto>? Attachments { get; set; }
}

public class MailWithFilesAndCcnRequest : CcnMailRequest
{
    [JsonPropertyName("attachments")] public List<AttachmentDto>? Attachments { get; set; }
}