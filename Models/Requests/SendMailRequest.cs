using System.ComponentModel.DataAnnotations;
using TopMail.Rest.Serialization;
using System.Text.Json.Serialization;

namespace TopMail.Rest.Models.Requests;

/// <summary>
/// Payload per invio email.
/// </summary>
/// <remarks>
/// Per body HTML usare <c>typeBody=1</c> (o <c>true</c>/<c>html</c>) e valorizzare <c>testoMail</c> con Base64 UTF-8.
/// </remarks>
public class SendMailRequest : IValidatableObject
{
    /// <summary>
    /// Mittente richiesto della mail.
    /// </summary>
    /// <example>noreply@example.com</example>
    [JsonPropertyName("mittente")]
    public string Mittente { get; set; } = string.Empty;

    /// <summary>
    /// Destinatari principali (To).
    /// </summary>
    /// <example>["utente@example.com"]</example>
    [JsonPropertyName("destinatario")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Destinatario { get; set; } = new();

    /// <summary>
    /// Destinatari in copia (Cc).
    /// </summary>
    /// <example>["audit@example.com"]</example>
    [JsonPropertyName("cc")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Cc { get; set; } = new();

    /// <summary>
    /// Destinatari in copia nascosta (Bcc/Ccn).
    /// </summary>
    /// <example>["bcc@example.com"]</example>
    [JsonPropertyName("ccn")]
    [JsonConverter(typeof(EmailAddressListJsonConverter))]
    public List<string> Ccn { get; set; } = new();

    /// <summary>
    /// Indirizzo Reply-To opzionale.
    /// </summary>
    /// <example>support@example.com</example>
    [JsonPropertyName("replyTo")]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Oggetto della mail.
    /// </summary>
    /// <example>Report giornaliero</example>
    [Required]
    [JsonPropertyName("oggetto")]
    public string Oggetto { get; set; } = string.Empty;

    /// <summary>
    /// Corpo del messaggio (testo o Base64 UTF-8 quando typeBody indica HTML).
    /// </summary>
    /// <example>PGgxPkNpb288L2gxPg==</example>
    [Required]
    [JsonPropertyName("testoMail")]
    public string TestoMail { get; set; } = string.Empty;

    /// <summary>
    /// Tipo body: 1/true/html per HTML, altrimenti testo semplice.
    /// </summary>
    /// <example>1</example>
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

