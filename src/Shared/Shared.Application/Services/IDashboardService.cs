using shop_back.src.Shared.Application.DTOs.Dashboard;

namespace shop_back.src.Shared.Application.Services
{
    public interface IDashboardService
    {
        /// <param name="userId">The caller; scopes the activity to them without read-all access.</param>
        /// <param name="access">Sections the caller may read.</param>
        /// <param name="days">Length of the activity series, ending today.</param>
        /// <param name="timeZone">IANA or Windows id used to split days; unknown ids fall back to UTC.</param>
        Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DashboardAccess access, int days, string? timeZone);
    }
}
