using System.Text.Json;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using StackExchange.Redis;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class OptionsService : IOptionsService
    {
        private readonly IUserLogRepository _userLogRepository;
        private readonly IDatabase _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);

        public OptionsService(IUserLogRepository userLogRepository, IConnectionMultiplexer redis)
        {
            _userLogRepository = userLogRepository;
            _cache = redis.GetDatabase();
        }

        public async Task<IEnumerable<SelectOptionDto>> GetOptionsAsync(string type, SelectRequestDto req)
        {
            req ??= new SelectRequestDto();

            string cacheKey = $"Options:{type}:{req.Search}:{req.Skip}:{req.Limit}";

            // 🔹 Try cache first
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
                return JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();

            IEnumerable<SelectOptionDto> result = type.ToLower() switch
            {
                "userlogcollections" => await _userLogRepository.GetDistinctModelNamesAsync(req),
                "userlogactiontypes" => await _userLogRepository.GetDistinctActionTypesAsync(req),
                "userlogcreators" => await _userLogRepository.GetDistinctCreatorsAsync(req),
                _ => new List<SelectOptionDto>()
            };

            // 🔹 Normalize label
            foreach (var item in result)
                item.Label = LabelFormatter.ToReadable(item.Label);

            // 🔹 Set cache
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), _cacheTtl);

            return result;
        }
    }
}
