namespace TopMail.Rest.Options;

public class AttachmentSecurityOptions
{
    public int MaxAttachmentCount { get; set; } = 10;
    public long MaxAttachmentSizeBytes { get; set; } = 5_000_000;
    public long MaxTotalAttachmentSizeBytes { get; set; } = 15_000_000;
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx", ".zip",
        ".png", ".jpg", ".jpeg", ".gif"
    ];
    public string[] BlockedExtensions { get; set; } =
    [
        ".exe", ".bat", ".cmd", ".com", ".js", ".vbs", ".ps1", ".msi", ".scr", ".pif"
    ];
}
