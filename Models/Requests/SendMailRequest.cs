using System.ComponentModel.DataAnnotations;
using TopMail.Rest.Serialization;
using System.Text.Json.Serialization;

namespace TopMail.Rest.Models.Requests;

public class SendMailRequest : IValidatableObject
{
    [JsonPropertyName("mittente")]
    public string Mittente { get; set; } = string.Empty;

    [JsonPropertyName("destinatario")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Destinatario { get; set; } = new();

    [JsonPropertyName("cc")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Cc { get; set; } = new();

    [JsonPropertyName("ccn")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Ccn { get; set; } = new();

    [JsonPropertyName("replyTo")]
    public string? ReplyTo { get; set; }

    [Required]
    [JsonPropertyName("oggetto")]
    public string Oggetto { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("testoMail")]
    public string TestoMail { get; set; } = string.Empty;

    [JsonPropertyName("typeBody")]
    public string? TypeBody { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Destinatario.Count == 0)
        {
            yield return new ValidationResult(
                "Almeno un destinatario e' obbligatorio.",
                [nameof(Destinatario)]);
        }
    }
}

