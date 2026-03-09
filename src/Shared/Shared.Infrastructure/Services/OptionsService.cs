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

            // Console.WriteLine($"========== OptionsService.GetOptionsAsync ==========");
            // Console.WriteLine($"Type: {type}");
            // Console.WriteLine($"Full req: {JsonSerializer.Serialize(req)}");
            // Console.WriteLine($"Search: '{req.Search}'");
            // Console.WriteLine($"Skip: {req.Skip}, Limit: {req.Limit}");
            // Console.WriteLine($"SortBy: {req.SortBy}, SortOrder: {req.SortOrder}");
            // Console.WriteLine($"Where count: {req.Where?.Count ?? 0}");
            // if (req.Where != null)
            // {
            //     foreach (var kvp in req.Where)
            //     {
            //         Console.WriteLine($"  Where[{kvp.Key}]: {kvp.Value}");
            //     }
            // }

            // 🔥 FIX: Include Where in cache key
            string whereJson = req.Where != null ? JsonSerializer.Serialize(req.Where) : "";
            string cacheKey = $"Options:{type}:{req.Search}:{req.Skip}:{req.Limit}:{req.SortBy}:{req.SortOrder}:{whereJson}";

            // Try cache first
            var cached = await _cache.StringGetAsync(cacheKey);
            List<SelectOptionDto> result;

            if (cached.HasValue)
            {
                // Console.WriteLine($"Cache HIT for {type}");
                result = JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();
                return result;
            }

            // Console.WriteLine($"Cache MISS for {type}, fetching from repository...");

            switch (type.ToLower())
            {
                case "userlogcollections":
                    Console.WriteLine("Calling _userLogRepository.GetDistinctModelNamesAsync");
                    result = (await _userLogRepository.GetDistinctModelNamesAsync(req)).ToList();
                    break;
                    
                case "userlogactiontypes":
                    Console.WriteLine("Calling _userLogRepository.GetDistinctActionTypesAsync");
                    result = (await _userLogRepository.GetDistinctActionTypesAsync(req)).ToList();
                    break;
                    
                case "userlogcreators":
                    // Console.WriteLine("Calling _userLogRepository.GetDistinctCreatorsAsync");
                    result = (await _userLogRepository.GetDistinctCreatorsAsync(req)).ToList();
                    break;
                    
                case "usercreators":
                    // Console.WriteLine("Calling _userRepository.GetDistinctCreatorsAsync");
                    result = (await _userRepository.GetDistinctCreatorsAsync(req)).ToList();
                    break;
                    
                case "userupdaters":
                    // Console.WriteLine("Calling _userRepository.GetDistinctUpdatersAsync");
                    result = (await _userRepository.GetDistinctUpdatersAsync(req)).ToList();
                    break;
                    
                case "userdatetypes":
                    // Console.WriteLine("Calling _userRepository.GetDistinctDateTypesAsync");
                    result = (await _userRepository.GetDistinctDateTypesAsync(req)).ToList();
                    break;
                    
                case "roles":
                    // Console.WriteLine("Calling _rolePermissionRepository.GetAllRolesAsync");
                    var roles = await _rolePermissionRepository.GetAllRolesAsync();
                    result = roles.Select(r => new SelectOptionDto { Value = r, Label = r }).ToList();
                    break;
                    
                case "permissions":
                    // Console.WriteLine("Calling _rolePermissionRepository.GetAllPermissionsAsync");
                    var permissions = await _rolePermissionRepository.GetAllPermissionsAsync();
                    result = permissions.Select(p => new SelectOptionDto { Value = p, Label = p }).ToList();
                    break;
                    
                default:
                    // Console.WriteLine($"Unknown type: {type}");
                    result = new List<SelectOptionDto>();
                    break;
            }

            // Console.WriteLine($"Repository returned {result.Count} items");

            // Normalize label
            foreach (var item in result)
            {
                item.Label = LabelFormatter.ToReadable(item.Label);
            }

            // Cache the result
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), _cacheTtl);
            // Console.WriteLine($"Cached result for {type}");

            return result;
        }
    }
}
