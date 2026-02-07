using StackExchange.Redis;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.UserLogs;
using System.Text.Json;
using shop_back.src.Shared.Infrastructure.Helpers;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserLogService : IUserLogService
    {
        private readonly IUserLogRepository _repository;
        private readonly IDatabase _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1); // adjust expiry

        public UserLogService(IUserLogRepository repository, IConnectionMultiplexer redis)
        {
            _repository = repository;
              _cache = redis.GetDatabase();
        }

        public async Task<UserLog> CreateLogAsync(
            Guid createdBy,
            string action,
            string? detail = null,
            string? changes = null,
            string? modelName = null,
            Guid? modelId = null,
            string? ip = null,
            string? browser = null,
            string? device = null,
            string? os = null,
            string? userAgent = null)
        {
            var log = new UserLog
            {
                Id = Guid.NewGuid(),
                CreatedBy = createdBy,
                ActionType = action,
                Detail = detail,
                Changes = changes,
                ModelName = modelName ?? string.Empty,
                ModelId = modelId,
                IpAddress = ip,
                Browser = browser,
                Device = device,
                OperatingSystem = os,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
                CreatedAtId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await _repository.CreateAsync(log);
            await _repository.SaveChangesAsync();

            return log;
        }

        public async Task<object> GetFilteredLogsAsync(UserLogFilterRequest req)
        {
            var (logs, totalCount, grandTotalCount, pageIndex, pageSize) = await _repository.GetFilteredAsync(req);

            return new
            {
                logs,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public Task<IEnumerable<UserLog>> GetLogsByUserAsync(Guid createdBy)
            => _repository.GetByUserIdAsync(createdBy);

        public Task<UserLog?> GetLogAsync(Guid id)
            => _repository.GetByIdAsync(id);

        private async Task<IEnumerable<SelectOptionDto>> GetOrSetCacheAsync(
            string cacheKey,
            Func<Task<IEnumerable<SelectOptionDto>>> factory)
        {
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();
            }

            var result = await factory();

            // Format labels
            foreach (var item in result)
            {
                item.Label = LabelFormatter.ToReadable(item.Label);
            }

            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), _cacheTtl);
            return result;
        }

        // public Task<IEnumerable<SelectOptionDto>> GetCollectionsAsync(SelectRequestDto req)
        // {
        //     var cacheKey = "UserLog:Collections";
        //     return GetOrSetCacheAsync(cacheKey, () => _repository.GetDistinctModelNamesAsync(req));
        // }

        // public Task<IEnumerable<SelectOptionDto>> GetActionTypesAsync(SelectRequestDto req)
        // {
        //     var cacheKey = "UserLog:ActionTypes";
        //     return GetOrSetCacheAsync(cacheKey, () => _repository.GetDistinctActionTypesAsync(req));
        // }

        // public Task<IEnumerable<SelectOptionDto>> GetCreatorsAsync(SelectRequestDto req)
        // {
        //     var cacheKey = "UserLog:Creators";
        //     return GetOrSetCacheAsync(cacheKey, () => _repository.GetDistinctCreatorsAsync(req));
        // }
    }
}
