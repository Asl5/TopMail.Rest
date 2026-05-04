using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TopMail.Rest.Options;

namespace TopMail.Rest.Security;

public class ApiKeyHmacAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string FailureReasonItemKey = "auth_failure_reason";
    private readonly IOptionsMonitor<AuthorizedClientsOptions> _clientsOptions;
    private readonly INonceReplayProtector _nonceReplayProtector;

    public ApiKeyHmacAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<AuthorizedClientsOptions> clientsOptions,
        INonceReplayProtector nonceReplayProtector)
        : base(options, logger, encoder)
    {
        _clientsOptions = clientsOptions;
        _nonceReplayProtector = nonceReplayProtector;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var missingHeaders = new List<string>();

        string? ReadOrTrack(string headerName)
        {
            var value = ReadRequiredHeader(headerName);
            if (value is null)
                missingHeaders.Add(headerName);

            return value;
        }

        var clientId = ReadOrTrack(ApiKeyHmacAuthenticationDefaults.ClientIdHeader);
        var apiKey = ReadOrTrack(ApiKeyHmacAuthenticationDefaults.ApiKeyHeader);
        var timestampRaw = ReadOrTrack(ApiKeyHmacAuthenticationDefaults.TimestampHeader);
        var nonce = ReadOrTrack(ApiKeyHmacAuthenticationDefaults.NonceHeader);
        var signatureRaw = ReadOrTrack(ApiKeyHmacAuthenticationDefaults.SignatureHeader);

        if (missingHeaders.Count > 0 || clientId is null || apiKey is null || timestampRaw is null || nonce is null || signatureRaw is null)
        {
            if (missingHeaders.Count > 0)
                return Fail($"Header autenticazione mancanti o non validi: {string.Join(", ", missingHeaders)}.");

            return Fail("Header autenticazione mancanti o non validi.");
        }

        var options = _clientsOptions.CurrentValue;
        var client = options.Clients.FirstOrDefault(x =>
            x.ClientId.Equals(clientId, StringComparison.OrdinalIgnoreCase));

        if (client is null || !client.Enabled)
            return Fail("Client non autorizzato.");

        if (!SecureEquals(apiKey, client.ApiKey))
            return Fail("ApiKey non valida.");

        if (!DateTimeOffset.TryParse(
                timestampRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return Fail("Timestamp non valido.");
        }

        var now = DateTimeOffset.UtcNow;
        var allowedSkew = TimeSpan.FromSeconds(options.AllowedClockSkewSeconds);
        if (timestamp < now.Subtract(allowedSkew) || timestamp > now.Add(allowedSkew))
            return Fail("Timestamp fuori finestra.");

        if (nonce.Length is < 8 or > 200)
            return Fail("Nonce non valido.");

        var bodyBytes = await ReadRequestBodyAsync();
        var bodyHash = ComputeBodyHash(bodyBytes);
        var pathAndQuery = Request.Path.Value + Request.QueryString.Value;
        var canonical = BuildCanonicalRequest(
            Request.Method,
            pathAndQuery ?? "/",
            bodyHash,
            timestampRaw,
            nonce,
            client.ClientId);

        var expectedSignature = ComputeHmac(client.HmacSecret, canonical);
        if (!TryReadBase64(signatureRaw, out var actualSignature))
            return Fail("Formato signature non valido.");

        if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
            return Fail("Signature non valida.");

        var nonceTtl = TimeSpan.FromSeconds(Math.Max(options.NonceTtlSeconds, options.AllowedClockSkewSeconds));
        if (!_nonceReplayProtector.TryReserve(client.ClientId, nonce, nonceTtl))
            return Fail("Replay rilevato.");

        var claims = CreateClaims(client);
        var identity = new ClaimsIdentity(claims, ApiKeyHmacAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyHmacAuthenticationDefaults.SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate =
            $"{ApiKeyHmacAuthenticationDefaults.SchemeName} realm=\"TopMail.Rest\"";

        if (Response.HasStarted)
            return Task.CompletedTask;

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        var reason = Context.Items.TryGetValue(FailureReasonItemKey, out var value) && value is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : "Unauthorized";

        return Response.WriteAsJsonAsync(new
        {
            success = false,
            code = "UNAUTHORIZED",
            message = reason
        });
    }

    private string? ReadRequiredHeader(string name)
    {
        if (!Request.Headers.TryGetValue(name, out var values))
            return null;

        var value = values.ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task<byte[]> ReadRequestBodyAsync()
    {
        Request.EnableBuffering();
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, Context.RequestAborted);
        Request.Body.Position = 0;
        return stream.ToArray();
    }

    private static string ComputeBodyHash(byte[] bodyBytes)
    {
        var hash = SHA256.HashData(bodyBytes);
        return Convert.ToBase64String(hash);
    }

    private static string BuildCanonicalRequest(
        string method,
        string pathAndQuery,
        string bodyHash,
        string timestamp,
        string nonce,
        string clientId)
    {
        return string.Join(
            '\n',
            method.ToUpperInvariant(),
            pathAndQuery,
            bodyHash,
            timestamp,
            nonce,
            clientId);
    }

    private static byte[] ComputeHmac(string secret, string canonical)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var canonicalBytes = Encoding.UTF8.GetBytes(canonical);
        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(canonicalBytes);
    }

    private static bool TryReadBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static bool SecureEquals(string value, string expected)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    private AuthenticateResult Fail(string reason)
    {
        Context.Items[FailureReasonItemKey] = reason;
        return AuthenticateResult.Fail(reason);
    }

    private static IEnumerable<Claim> CreateClaims(AuthorizedClientOptions client)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, client.ClientId);
        yield return new Claim("client_id", client.ClientId);
        yield return new Claim("client_max_rpm", client.MaxRequestsPerMinute.ToString(CultureInfo.InvariantCulture));

        foreach (var from in client.AllowedFromAddresses.Where(x => !string.IsNullOrWhiteSpace(x)))
            yield return new Claim("allowed_from", from.Trim());

        foreach (var domain in client.AllowedRecipientDomains.Where(x => !string.IsNullOrWhiteSpace(x)))
            yield return new Claim("allowed_domain", NormalizeDomain(domain));
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimStart('@').ToLowerInvariant();
    }
}
