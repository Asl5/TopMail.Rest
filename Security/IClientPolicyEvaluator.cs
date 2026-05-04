using System.Security.Claims;
using TopMail.Rest.Models.Requests;

namespace TopMail.Rest.Security;

public interface IClientPolicyEvaluator
{
    bool TryValidate(ClaimsPrincipal principal, SendMailRequest request, out string error);
}
