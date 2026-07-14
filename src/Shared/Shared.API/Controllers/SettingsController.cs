// D:\shop\shop_back\src\Shared\Shared.API\Controllers\SettingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Settings;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly IAppSettingService _settingsService;

        public SettingsController(IAppSettingService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Get all settings grouped by category (Admin only)
        /// </summary>
        [HttpGet]
        [HasPermissionAny("read-admin-settings")]
        public async Task<IActionResult> GetAllSettings()
        {
            var result = await _settingsService.GetAllSettingsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get settings for a specific category
        /// </summary>
        [HttpGet("category/{category}")]
        [HasPermissionAny("read-admin-settings")]
        public async Task<IActionResult> GetSettingsByCategory(string category)
        {
            var result = await _settingsService.GetSettingsByCategoryAsync(category);
            return Ok(result);
        }

        /// <summary>
        /// Get public settings (Theme & Branding) - No authentication required
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicSettings()
        {
            var result = await _settingsService.GetPublicSettingsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get a single setting by category and key
        /// </summary>
        [HttpGet("{category}/{key}")]
        [HasPermissionAny("read-admin-settings")]
        public async Task<IActionResult> GetSetting(string category, string key)
        {
            var result = await _settingsService.GetSettingAsync(category, key);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>
        /// Update a single setting
        /// </summary>
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-settings")]
        public async Task<IActionResult> UpdateSetting(Guid id, [FromBody] UpdateSettingRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _settingsService.UpdateSettingAsync(id, request.Value, currentUserId);
            return Ok(result);
        }

        /// <summary>
        /// Update multiple settings in a category
        /// </summary>
        [HttpPut("category/{category}")]
        [HasPermissionAny("update-admin-settings")]
        public async Task<IActionResult> UpdateCategorySettings(string category, [FromBody] Dictionary<string, object> settings)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _settingsService.UpdateCategorySettingsAsync(category, settings, currentUserId);
            return Ok(new { success = result, message = result ? "Settings updated successfully" : "No changes were made" });
        }

        /// <summary>
        /// Reset a category to default values
        /// </summary>
        [HttpPost("reset/{category}")]
        [HasPermissionAny("update-admin-settings")]
        public async Task<IActionResult> ResetCategory(string category)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _settingsService.ResetCategoryToDefaultAsync(category, currentUserId);
            if (!result) return BadRequest(new { message = "Failed to reset settings" });
            
            return Ok(new { message = "Settings reset to defaults" });
        }

        /// <summary>
        /// Clear settings cache
        /// </summary>
        [HttpPost("clear-cache")]
        [HasPermissionAny("update-admin-settings")]
        public async Task<IActionResult> ClearCache()
        {
            var result = await _settingsService.ClearCacheAsync();
            return Ok(new { success = result });
        }
    }

    public class UpdateSettingRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}