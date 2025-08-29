using StackExchange.Redis;
using System.Text.Json;
using shop_back.App.Models;
using shop_back.App.DTOs;
using shop_back.App.Repositories;

namespace shop_back.App.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly ITranslationRepository _repo;
        private readonly IDatabase _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(60);

        public TranslationService(ITranslationRepository repo, IConnectionMultiplexer redis)
        {
            _repo = repo;
            _cache = redis.GetDatabase();
        }

        private static string CacheKey(string lang, string? module) => $"translations:{lang}:{module ?? "common"}";

        public async Task<IDictionary<string, string>> GetTranslationsAsync(string lang, string? module = null, CancellationToken ct = default)
        {
            var cacheKey = CacheKey(lang, module);
            // 1. Try cache
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(cached!) ?? new Dictionary<string,string>();
            }

            // 2. Read from DB
            var rows = await _repo.GetByLangAsync(lang, module, ct);
            var map = rows.ToDictionary(r => $"{r.Key.Module}.{r.Key.Key}", r => r.Value);

            // 3. Cache the result
            var serialized = JsonSerializer.Serialize(map);
            await _cache.StringSetAsync(cacheKey, serialized, _cacheTtl);

            return map;
        }
    }
}
