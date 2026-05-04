namespace TopMail.Rest.Options;

public class AuthorizedClientsOptions
{
    public int AllowedClockSkewSeconds { get; set; } = 300;
    public int NonceTtlSeconds { get; set; } = 300;
    public List<AuthorizedClientOptions> Clients { get; set; } = new();
}

public class AuthorizedClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string HmacSecret { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerMinute { get; set; } = 60;
    public string[] AllowedFromAddresses { get; set; } = Array.Empty<string>();
    public string[] AllowedRecipientDomains { get; set; } = Array.Empty<string>();
}
