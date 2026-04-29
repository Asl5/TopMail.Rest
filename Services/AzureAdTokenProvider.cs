using Microsoft.Identity.Client;
using Microsoft.Extensions.Options;
using TopMail.Rest.Options;

namespace TopMail.Rest.Services;

public class AzureAdTokenProvider
{
    private readonly AzureAdOptions _opts;
    private readonly IConfidentialClientApplication _app;
    private AuthenticationResult? _cached;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private static readonly string[] Scopes = new[] { "https://outlook.office365.com/.default" };

    public AzureAdTokenProvider(IOptions<AzureAdOptions> options)
    {
        _opts = options.Value;
        _app = ConfidentialClientApplicationBuilder
            .Create(_opts.ClientId)
            .WithClientSecret(_opts.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_opts.TenantId}/v2.0")
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            if (_cached != null && _cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                return _cached.AccessToken;

            _cached = await _app.AcquireTokenForClient(Scopes).ExecuteAsync(ct);
            return _cached.AccessToken;
        }
        finally
        {
            _mutex.Release();
        }
    }
}
