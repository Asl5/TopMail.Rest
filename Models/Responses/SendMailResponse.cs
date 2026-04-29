using System.Text.Json.Serialization;

namespace TopMail.Rest.Models.Responses;

public class SendMailResponse
{
    [JsonIgnore]
    public int HttpStatus { get; init; } = 200;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("fallbackUsed")]
    public bool FallbackUsed { get; init; }

    [JsonPropertyName("initialError")]
    public string? InitialError { get; init; }

    [JsonPropertyName("requestedFrom")]
    public string? RequestedFrom { get; init; }

    [JsonPropertyName("effectiveFrom")]
    public string? EffectiveFrom { get; init; }
}
