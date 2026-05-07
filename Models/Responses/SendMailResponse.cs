using System.Text.Json.Serialization;

namespace TopMail.Rest.Models.Responses;

/// <summary>
/// Risposta standard dell'API di invio email.
/// </summary>
/// <remarks>
/// In caso di errore applicativo, il dettaglio e' valorizzato nel campo <c>message</c>.
/// </remarks>
public class SendMailResponse
{
    /// <summary>
    /// HTTP status interno usato dal controller per impostare la response.
    /// </summary>
    [JsonIgnore]
    public int HttpStatus { get; init; } = 200;

    /// <summary>
    /// True se l'invio e' andato a buon fine.
    /// </summary>
    /// <example>true</example>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// Codice applicativo di esito.
    /// </summary>
    /// <example>1</example>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Messaggio descrittivo dell'esito.
    /// </summary>
    /// <example>Mail inviata correttamente.</example>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// True se e' stato usato il fallback mittente.
    /// </summary>
    [JsonPropertyName("fallbackUsed")]
    public bool FallbackUsed { get; init; }

    /// <summary>
    /// Errore iniziale che ha causato il fallback, se presente.
    /// </summary>
    [JsonPropertyName("initialError")]
    public string? InitialError { get; init; }

    /// <summary>
    /// Mittente richiesto dal client.
    /// </summary>
    [JsonPropertyName("requestedFrom")]
    public string? RequestedFrom { get; init; }

    /// <summary>
    /// Mittente effettivamente usato in invio.
    /// </summary>
    [JsonPropertyName("effectiveFrom")]
    public string? EffectiveFrom { get; init; }
}
