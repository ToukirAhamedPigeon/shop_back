using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.UserLogs;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserLogService : IUserLogService
    {
        private readonly IUserLogRepository _repository;

        public UserLogService(IUserLogRepository repository)
        {
            _repository = repository;
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
            var (logs, totalCount, pageIndex, pageSize) = await _repository.GetFilteredAsync(req);

            return new
            {
                logs,
                totalCount,
                pageIndex,
                pageSize
            };
        }

        public Task<IEnumerable<UserLog>> GetLogsByUserAsync(Guid createdBy)
            => _repository.GetByUserIdAsync(createdBy);

        public Task<UserLog?> GetLogAsync(Guid id)
            => _repository.GetByIdAsync(id);
    }
}
