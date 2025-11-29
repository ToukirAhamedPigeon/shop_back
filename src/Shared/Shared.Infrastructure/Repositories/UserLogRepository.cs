using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Application.DTOs.UserLogs;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserLogRepository : IUserLogRepository
    {
        private readonly AppDbContext _context;

        public UserLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserLog> CreateAsync(UserLog log)
        {
            await _context.UserLogs.AddAsync(log);
            return log;
        }

        public async Task<UserLog?> GetByIdAsync(Guid id)
        {
            return await _context.UserLogs.FindAsync(id);
        }

        public async Task<IEnumerable<UserLog>> GetByUserIdAsync(Guid createdBy)
        {
            return await _context.UserLogs
                .Where(l => l.CreatedBy == createdBy)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserLog>> GetAllAsync()
        {
            return await _context.UserLogs
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<(IEnumerable<UserLogDto> Logs, int TotalCount, int PageIndex, int PageSize)> GetFilteredAsync(UserLogFilterRequest req)
        {
            var query = _context.UserLogs.AsQueryable();

            // 🔥 Search filter
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                query = query.Where(x =>
                    (x.Detail ?? "").Contains(req.Q) ||
                    x.ActionType.Contains(req.Q) ||
                    x.ModelName.Contains(req.Q)
                );
            }

            // 🔥 Date filters
            if (req.CreatedAtFrom != null)
                query = query.Where(x => x.CreatedAt >= req.CreatedAtFrom.Value.Date);

            if (req.CreatedAtTo != null)
                query = query.Where(x => x.CreatedAt <= req.CreatedAtTo.Value.Date.AddDays(1).AddSeconds(-1));

            // 🔥 Collection filter
            if (req.CollectionName?.Length > 0)
                query = query.Where(x => req.CollectionName.Contains(x.ModelName));

            // 🔥 Action Type filter
            if (req.ActionType?.Length > 0)
                query = query.Where(x => req.ActionType.Contains(x.ActionType));

            // 🔥 Created By filter
            if (req.CreatedBy?.Length > 0)
                query = query.Where(x => req.CreatedBy.Contains(x.CreatedBy.ToString()));

            // 🔥 Include User data for CreatedByName
            var logsQuery = query
                .Join(_context.Users,
                    log => log.CreatedBy,
                    user => user.Id,
                    (log, user) => new UserLogDto
                    {
                        Id = log.Id,
                        Detail = log.Detail,
                        ModelName = log.ModelName,
                        ActionType = log.ActionType,
                        ModelId = log.ModelId,
                        CreatedBy = log.CreatedBy,
                        CreatedByName = user.Name, 
                        CreatedAt = log.CreatedAt,
                        Changes = log.Changes,
                        CreatedAtId = log.CreatedAtId,
                        IpAddress = log.IpAddress,
                        Browser = log.Browser,
                        Device = log.Device,
                        OperatingSystem=log.OperatingSystem,
                        UserAgent=log.UserAgent
                    });

            int totalCount = await logsQuery.CountAsync();

            // Sorting
            bool desc = req.SortOrder?.ToLower() == "desc";

            logsQuery = req.SortBy?.ToLower() switch
            {
                "createdat" => desc ? logsQuery.OrderByDescending(x => x.CreatedAt) : logsQuery.OrderBy(x => x.CreatedAt),
                "actiontype" => desc ? logsQuery.OrderByDescending(x => x.ActionType) : logsQuery.OrderBy(x => x.ActionType),
                _ => logsQuery.OrderByDescending(x => x.CreatedAt)
            };

            // Pagination
            var logs = await logsQuery
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();

            return (logs, totalCount, req.Page - 1, req.Limit); 
        }
    }
}
