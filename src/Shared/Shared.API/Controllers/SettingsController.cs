// D:\shop\shop_back\src\Shared\Shared.API\Controllers\SettingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Settings;
using shop_back.src.Shared.Application.Services;
using System.IdentityModel.Tokens.Jwt;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly IUserSettingService _settingsService;

        public SettingsController(IUserSettingService settingsService)
        {
            _settingsService = settingsService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User?.FindFirst("UserId")?.Value 
                              ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("User not authenticated");
            
            return userId;
        }

        /// <summary>
        /// Get all settings (User settings + Branding)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.GetAllSettingsAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Get user settings (Theme + General)
        /// </summary>
        [HttpGet("user")]
        public async Task<IActionResult> GetUserSettings()
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.GetUserSettingsAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Get branding settings (Global)
        /// </summary>
        [HttpGet("branding")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBrandingSettings()
        {
            var result = await _settingsService.GetBrandingSettingsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Update Theme settings
        /// </summary>
        [HttpPut("theme")]
        public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeSettingsDto settings)
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.UpdateThemeSettingsAsync(userId, settings, userId.ToString());
            return Ok(result);
        }

        /// <summary>
        /// Update General settings
        /// </summary>
        [HttpPut("general")]
        public async Task<IActionResult> UpdateGeneral([FromBody] UpdateGeneralSettingsDto settings)
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.UpdateGeneralSettingsAsync(userId, settings, userId.ToString());
            return Ok(result);
        }

        /// <summary>
        /// Update Branding settings (Developer only)
        /// </summary>
        [HttpPut("branding")]
        [Authorize(Roles = "developer")]
        public async Task<IActionResult> UpdateBranding([FromBody] UpdateBrandingSettingsDto settings)
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.UpdateBrandingSettingsAsync(settings, userId.ToString());
            return Ok(result);
        }

        /// <summary>
        /// Reset Theme settings to defaults
        /// </summary>
        [HttpPost("reset/theme")]
        public async Task<IActionResult> ResetTheme()
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.ResetThemeSettingsAsync(userId, userId.ToString());
            return Ok(result);
        }

        /// <summary>
        /// Reset General settings to defaults
        /// </summary>
        [HttpPost("reset/general")]
        public async Task<IActionResult> ResetGeneral()
        {
            var userId = GetCurrentUserId();
            var result = await _settingsService.ResetGeneralSettingsAsync(userId, userId.ToString());
            return Ok(result);
        }
    }
}