namespace TopMail.Rest.Options;

public class PlainTextRecipientOptions
{
    public const string SectionName = "PlainTextRecipients";

    public bool Enabled { get; set; }
    public string[] Domains { get; set; } = [];
    public string[] Addresses { get; set; } = [];
}
