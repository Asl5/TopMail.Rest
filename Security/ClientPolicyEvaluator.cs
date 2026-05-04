using System.Net.Mail;
using System.Security.Claims;
using TopMail.Rest.Models.Requests;

namespace TopMail.Rest.Security;

public class ClientPolicyEvaluator : IClientPolicyEvaluator
{
    public bool TryValidate(ClaimsPrincipal principal, SendMailRequest request, out string error)
    {
        error = string.Empty;
        var allowedFrom = principal.FindAll("allowed_from").Select(x => x.Value).ToArray();
        var allowedDomains = principal.FindAll("allowed_domain").Select(x => NormalizeDomain(x.Value)).ToArray();

        if (allowedFrom.Length > 0)
        {
            var requestedFrom = request.Mittente?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requestedFrom))
            {
                error = "Mittente obbligatorio per il client autenticato.";
                return false;
            }

            if (!allowedFrom.Contains(requestedFrom, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Mittente '{requestedFrom}' non consentito per il client autenticato.";
                return false;
            }
        }

        if (allowedDomains.Length == 0)
            return true;

        foreach (var address in GetRecipients(request))
        {
            if (!MailAddress.TryCreate(address, out var parsed))
                continue;

            var domain = NormalizeDomain(parsed.Host);
            if (!allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Destinatario '{parsed.Address}' non consentito per il client autenticato.";
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> GetRecipients(SendMailRequest request)
    {
        return Expand(request.Destinatario)
            .Concat(Expand(request.Cc))
            .Concat(Expand(request.Ccn));
    }

    private static IEnumerable<string> Expand(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var split = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var item in split)
                yield return item;
        }
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimStart('@').ToLowerInvariant();
    }
}
