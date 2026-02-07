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
        private readonly IUserRepository _userRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly IDatabase _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);

        public OptionsService(
            IUserLogRepository userLogRepository, 
            IUserRepository userRepository, 
            IRolePermissionRepository rolePermissionRepository,
            IConnectionMultiplexer redis)
        {
            _userLogRepository = userLogRepository;
            _userRepository = userRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _cache = redis.GetDatabase();
        }

        public async Task<IEnumerable<SelectOptionDto>> GetOptionsAsync(string type, SelectRequestDto req)
        {
            req ??= new SelectRequestDto();

            string cacheKey = $"Options:{type}:{req.Search}:{req.Skip}:{req.Limit}";

            // 🔹 Try cache first
            var cached = await _cache.StringGetAsync(cacheKey);
            List<SelectOptionDto> result;

            if (cached.HasValue)
            {
                // Deserialize cached data
                result = JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();

                // 🔹 Normalize labels even for cached items
                foreach (var item in result)
                    item.Label = LabelFormatter.ToReadable(item.Label);

                // 🔹 Optionally, refresh cache with normalized labels
                await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), _cacheTtl);

                return result;
            }
            result = type.ToLower() switch
            {
                "userlogcollections" => (await _userLogRepository.GetDistinctModelNamesAsync(req)).ToList(),
                "userlogactiontypes" => (await _userLogRepository.GetDistinctActionTypesAsync(req)).ToList(),
                "userlogcreators" => (await _userLogRepository.GetDistinctCreatorsAsync(req)).ToList(),
                "usercreators" => (await _userRepository.GetDistinctCreatorsAsync(req)).ToList(),
                "userupdaters" => (await _userRepository.GetDistinctUpdatersAsync(req)).ToList(),
                "userdatetypes" => (await _userRepository.GetDistinctDateTypesAsync(req)).ToList(),
                "roles" => (await _rolePermissionRepository.GetAllRolesAsync())
                            .Select(r => new SelectOptionDto { Value = r, Label = r })
                            .ToList(),

                "permissions" => (await _rolePermissionRepository.GetAllPermissionsAsync())
                                  .Select(p => new SelectOptionDto { Value = p, Label = p })
                                  .ToList(),
                _ => new List<SelectOptionDto>()
            };

            // 🔹 Normalize label
            foreach (var item in result)
            {
                item.Label = LabelFormatter.ToReadable(item.Label);
            }

            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), _cacheTtl);

            return result;
        }
    }
}
