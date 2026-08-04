using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace TaskManager.Api.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache distributedCache, ILogger<CacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cachedString = await _distributedCache.GetStringAsync(key);
            return string.IsNullOrEmpty(cachedString) ? default : JsonSerializer.Deserialize<T>(cachedString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed Cache read failure for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        try
        {
            var serializedString = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromHours(1),
                SlidingExpirationRelativeToNow = slidingExpiration ?? TimeSpan.FromMinutes(10)
            };
            await _distributedCache.SetStringAsync(key, serializedString, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed Cache write failure for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _distributedCache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed Cache eviction failure for key: {Key}", key);
        }
    }
}
