using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Dashboard;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IAuthorizationService _authorization;

        public DashboardController(IDashboardService dashboardService, IAuthorizationService authorization)
        {
            _dashboardService = dashboardService;
            _authorization = authorization;
        }

        /// <summary>
        /// Everything the dashboard shows in one call. Sections the caller can't
        /// read come back as null.
        /// </summary>
        /// <param name="days">Days of activity, ending today (1–90, default 14).</param>
        /// <param name="timeZone">The browser's time zone, e.g. "Asia/Dhaka", so days split at local midnight.</param>
        [HttpGet("summary")]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
            [FromQuery] int days = ActivityDays.DefaultDays,
            [FromQuery] string? timeZone = null)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var access = new DashboardAccess(
                Users: await Can("read-admin-users"),
                Mail: await Can("read-admin-mails"),
                Logs: await Can("read-admin-user-logs"),
                AllLogs: await Can("read-admin-all-user-logs"));

            return Ok(await _dashboardService.GetSummaryAsync(userId, access, days, timeZone));
        }

        // Same policy the [HasPermissionAny] attribute builds, so the rules match the other endpoints.
        private async Task<bool> Can(string permission) =>
            (await _authorization.AuthorizeAsync(User, null, $"PERMISSION:Or:{permission}")).Succeeded;
    }
}
