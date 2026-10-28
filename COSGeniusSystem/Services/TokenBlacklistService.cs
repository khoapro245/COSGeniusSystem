using Microsoft.Extensions.Caching.Memory;

namespace COSGeniusSystem.Services;

public interface ITokenBlacklistService
{
    void BlacklistToken(string token, TimeSpan duration);
    bool IsTokenBlacklisted(string token);
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _memoryCache;
    private const string CacheKeyPrefix = "blacklisted_token_";

    public TokenBlacklistService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public void BlacklistToken(string token, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        
        var cacheKey = $"{CacheKeyPrefix}{token}";
        _memoryCache.Set(cacheKey, true, duration);
    }

    public bool IsTokenBlacklisted(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var cacheKey = $"{CacheKeyPrefix}{token}";
        return _memoryCache.TryGetValue(cacheKey, out _);
    }
}
