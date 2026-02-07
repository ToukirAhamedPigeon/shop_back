using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Application.DTOs.UserLogs;
using shop_back.src.Shared.Application.DTOs.Common;

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

        public async Task<(IEnumerable<UserLogDto> Logs, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> GetFilteredAsync(UserLogFilterRequest req)
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
            int grandTotalCount = await _context.UserLogs.CountAsync();


            // Sorting
            bool desc = req.SortOrder?.ToLower() == "desc";

           logsQuery = req.SortBy?.ToLower() switch
            {
                "createdat" => desc ? logsQuery.OrderByDescending(x => x.CreatedAt) : logsQuery.OrderBy(x => x.CreatedAt),
                "createdbyname" => desc ? logsQuery.OrderByDescending(x => x.CreatedByName) : logsQuery.OrderBy(x => x.CreatedByName), // <- updated
                "actiontype" => desc ? logsQuery.OrderByDescending(x => x.ActionType) : logsQuery.OrderBy(x => x.ActionType),
                "changes" => desc ? logsQuery.OrderByDescending(x => x.Changes) : logsQuery.OrderBy(x => x.Changes),
                "modelname" => desc ? logsQuery.OrderByDescending(x => x.ModelName) : logsQuery.OrderBy(x => x.ModelName),
                "operatingsystem" => desc ? logsQuery.OrderByDescending(x => x.OperatingSystem) : logsQuery.OrderBy(x => x.OperatingSystem),
                "browser" => desc ? logsQuery.OrderByDescending(x => x.Browser) : logsQuery.OrderBy(x => x.Browser),
                "device" => desc ? logsQuery.OrderByDescending(x => x.Device) : logsQuery.OrderBy(x => x.Device),
                "ipaddress" => desc ? logsQuery.OrderByDescending(x => x.IpAddress) : logsQuery.OrderBy(x => x.IpAddress),
                "useragent" => desc ? logsQuery.OrderByDescending(x => x.UserAgent) : logsQuery.OrderBy(x => x.UserAgent),
                _ => logsQuery.OrderByDescending(x => x.CreatedAt)
            };


            // Pagination
            var logs = await logsQuery
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();

            return (logs, totalCount, grandTotalCount, req.Page - 1, req.Limit); 
        }
    
        public async Task<IEnumerable<SelectOptionDto>> GetDistinctModelNamesAsync(SelectRequestDto req)
        {
            var query = _context.UserLogs.AsQueryable();

            // 🔹 Apply 'where' filters safely
            if (req.Where != null && req.Where.TryGetValue("ModelName", out var modelNameNode))
            {
                var modelName = modelNameNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(modelName))
                    query = query.Where(x => x.ModelName.Contains(modelName));
            }

            // 🔹 Apply search
            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.ModelName.Contains(req.Search));

            var result = await query
                .Select(x => x.ModelName)
                .Distinct()
                .OrderBy(x => x)
                .Skip(req.Skip)
                .Take(req.Limit)
                .Select(x => new SelectOptionDto { Value = x, Label = x })
                .ToListAsync();

            return result;
        }

        public async Task<IEnumerable<SelectOptionDto>> GetDistinctActionTypesAsync(SelectRequestDto req)
        {
            var query = _context.UserLogs.AsQueryable();

            if (req.Where != null && req.Where.TryGetValue("ActionType", out var actionTypeNode))
            {
                var actionType = actionTypeNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(actionType))
                    query = query.Where(x => x.ActionType.Contains(actionType));
            }

            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.ActionType.Contains(req.Search));

            var result = await query
                .Select(x => x.ActionType)
                .Distinct()
                .OrderBy(x => x)
                .Skip(req.Skip)
                .Take(req.Limit)
                .Select(x => new SelectOptionDto { Value = x, Label = x })
                .ToListAsync();

            Console.WriteLine($"ActionTypes: {string.Join(", ", result.Select(x => x.Label))}");
            return result;
        }

        public async Task<IEnumerable<SelectOptionDto>> GetDistinctCreatorsAsync(SelectRequestDto req)
        {
            var query = _context.UserLogs
                .Join(_context.Users,
                    log => log.CreatedBy,
                    user => user.Id,
                    (log, user) => new { log, user })
                .AsQueryable();

            if (req.Where != null && req.Where.TryGetValue("CreatedByName", out var createdByNameNode))
            {
                var createdByName = createdByNameNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(createdByName))
                    query = query.Where(x => x.user.Name.Contains(createdByName));
            }

            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.user.Name.Contains(req.Search));

            var result = await query
                .Select(x => new SelectOptionDto { Value = x.user.Id.ToString(), Label = x.user.Name })
                .Distinct()
                .OrderBy(x => x.Label)
                .Skip(req.Skip)
                .Take(req.Limit)
                .ToListAsync();

            return result;
        }
    }
}
