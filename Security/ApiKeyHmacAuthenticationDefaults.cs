namespace TopMail.Rest.Security;

public static class ApiKeyHmacAuthenticationDefaults
{
    public const string SchemeName = "ApiKeyHmac";
    public const string ClientIdHeader = "X-Client-Id";
    public const string ApiKeyHeader = "X-Api-Key";
    public const string TimestampHeader = "X-Timestamp";
    public const string NonceHeader = "X-Nonce";
    public const string SignatureHeader = "X-Signature";
}
