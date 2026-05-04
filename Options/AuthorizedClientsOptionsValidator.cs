using Microsoft.Extensions.Options;

namespace TopMail.Rest.Options;

public class AuthorizedClientsOptionsValidator : IValidateOptions<AuthorizedClientsOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthorizedClientsOptions options)
    {
        if (options.AllowedClockSkewSeconds <= 0)
            return ValidateOptionsResult.Fail("AuthorizedClients:AllowedClockSkewSeconds deve essere > 0.");

        if (options.NonceTtlSeconds <= 0)
            return ValidateOptionsResult.Fail("AuthorizedClients:NonceTtlSeconds deve essere > 0.");

        if (options.Clients.Count == 0)
            return ValidateOptionsResult.Fail("AuthorizedClients:Clients deve contenere almeno un client.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                return ValidateOptionsResult.Fail("AuthorizedClients:Clients:ClientId obbligatorio.");

            if (!seen.Add(client.ClientId))
                return ValidateOptionsResult.Fail($"ClientId duplicato: '{client.ClientId}'.");

            if (!client.Enabled)
                continue;

            if (string.IsNullOrWhiteSpace(client.ApiKey))
                return ValidateOptionsResult.Fail($"ApiKey obbligatoria per client '{client.ClientId}'.");

            if (string.IsNullOrWhiteSpace(client.HmacSecret))
                return ValidateOptionsResult.Fail($"HmacSecret obbligatoria per client '{client.ClientId}'.");

            if (client.MaxRequestsPerMinute <= 0)
                return ValidateOptionsResult.Fail($"MaxRequestsPerMinute non valido per client '{client.ClientId}'.");
        }

        return ValidateOptionsResult.Success;
    }
}
