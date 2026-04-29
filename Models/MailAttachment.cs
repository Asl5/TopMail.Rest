namespace TopMail.Rest.Models;

public class MailAttachment
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public bool IsInline { get; init; }
    public string? ContentId { get; init; }
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public long Length => Content.LongLength;
}
