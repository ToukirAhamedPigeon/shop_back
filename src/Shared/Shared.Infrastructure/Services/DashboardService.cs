using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.DTOs.Dashboard;
using shop_back.src.Shared.Application.DTOs.UserLogs;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Helpers;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private const int RecentLogCount = 6;

        private readonly AppDbContext _context;
        private readonly IMailRepository _mailRepository;

        public DashboardService(AppDbContext context, IMailRepository mailRepository)
        {
            _context = context;
            _mailRepository = mailRepository;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DashboardAccess access, int days, string? timeZone)
        {
            var tz = ActivityDays.ResolveTimeZone(timeZone);
            var summary = new DashboardSummaryDto { TimeZone = tz.Id };

            // Sequential on purpose: a DbContext can't run queries in parallel.
            if (access.Users)
            {
                summary.Users = new UsersSummaryDto
                {
                    // The global query filter already leaves out deleted users.
                    Total = await _context.Users.CountAsync(),
                    Active = await _context.Users.CountAsync(u => u.IsActive),
                };
            }

            if (access.Mail)
                summary.Mail = await _mailRepository.GetStatisticsAsync();

            if (access.Logs)
            {
                var ownOnly = !access.AllLogs;
                var logs = _context.UserLogs.AsNoTracking();
                if (ownOnly) logs = logs.Where(l => l.CreatedBy == userId);

                var dates = ActivityDays.LocalDates(tz, ActivityDays.ClampDays(days), DateTime.UtcNow);
                var (startUtc, endUtc) = ActivityDays.RangeUtc(dates, tz);
                // Only the timestamps come back; grouping happens in memory so
                // days follow the caller's time zone, DST included.
                var timestamps = await logs
                    .Where(l => l.CreatedAt >= startUtc && l.CreatedAt < endUtc)
                    .Select(l => l.CreatedAt)
                    .ToListAsync();

                summary.Activity = new ActivitySummaryDto
                {
                    OwnOnly = ownOnly,
                    Days = ActivityDays.Bucket(timestamps, dates, tz),
                };

                summary.RecentLogs = await logs
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(RecentLogCount)
                    .GroupJoin(_context.Users.IgnoreQueryFilters(), l => l.CreatedBy, u => u.Id, (l, users) => new { l, users })
                    .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new UserLogDto
                    {
                        Id = x.l.Id,
                        Detail = x.l.Detail,
                        ActionType = x.l.ActionType,
                        ModelName = x.l.ModelName,
                        ModelId = x.l.ModelId,
                        CreatedBy = x.l.CreatedBy,
                        CreatedByName = u != null ? u.Name : null,
                        CreatedAt = x.l.CreatedAt,
                        CreatedAtId = x.l.CreatedAtId,
                    })
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();
            }

            return summary;
        }
    }
}
