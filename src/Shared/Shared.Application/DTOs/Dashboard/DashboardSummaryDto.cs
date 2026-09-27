using shop_back.src.Shared.Application.DTOs.Mails;
using shop_back.src.Shared.Application.DTOs.UserLogs;

namespace shop_back.src.Shared.Application.DTOs.Dashboard
{
    /// <summary>
    /// Everything the admin dashboard shows, in one response. A section is null
    /// when the caller lacks the permission to read it.
    /// </summary>
    public class DashboardSummaryDto
    {
        /// <summary>The IANA/Windows time zone the activity days were bucketed in.</summary>
        public string TimeZone { get; set; } = "UTC";
        public UsersSummaryDto? Users { get; set; }
        public MailStatisticsDto? Mail { get; set; }
        public ActivitySummaryDto? Activity { get; set; }
        public List<UserLogDto>? RecentLogs { get; set; }
    }

    public class UsersSummaryDto
    {
        /// <summary>Users that aren't deleted.</summary>
        public int Total { get; set; }
        /// <summary>Of those, the ones marked active.</summary>
        public int Active { get; set; }
    }

    public class ActivitySummaryDto
    {
        /// <summary>True when only the caller's own actions are counted (no read-all permission).</summary>
        public bool OwnOnly { get; set; }
        /// <summary>One entry per local day, oldest first, ending today.</summary>
        public List<ActivityDayDto> Days { get; set; } = new();
    }

    public class ActivityDayDto
    {
        /// <summary>Local calendar date, formatted yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>Which sections the caller may see; resolved from their permissions.</summary>
    public record DashboardAccess(bool Users, bool Mail, bool Logs, bool AllLogs);
}
