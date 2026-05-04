using Microsoft.Extensions.Caching.Memory;

namespace TopMail.Rest.Security;

public class MemoryNonceReplayProtector : INonceReplayProtector
{
    private readonly IMemoryCache _cache;

    public MemoryNonceReplayProtector(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryReserve(string clientId, string nonce, TimeSpan ttl)
    {
        var key = $"nonce:{clientId}:{nonce}";
        if (_cache.TryGetValue(key, out _))
            return false;

        _cache.Set(key, true, ttl);
        return true;
    }
}
