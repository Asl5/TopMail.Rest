namespace TopMail.Rest.Security;

public interface INonceReplayProtector
{
    bool TryReserve(string clientId, string nonce, TimeSpan ttl);
}
