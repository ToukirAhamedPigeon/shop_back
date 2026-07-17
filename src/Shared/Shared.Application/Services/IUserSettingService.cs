// D:\shop\shop_back\src\Shared\Shared.Application\Services\IUserSettingService.cs
using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Application.DTOs.Settings;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserSettingService
    {
        // Get user settings (Theme + General) with Redis caching
        Task<UserSettingsDto> GetUserSettingsAsync(Guid userId);
        
        // Get branding settings (Global) with Redis caching
        Task<BrandingSettingsDto> GetBrandingSettingsAsync();
        
        // Get all settings (User + Branding)
        Task<SettingsResponseDto> GetAllSettingsAsync(Guid userId);
        
        // Update Theme settings
        Task<UserSettingsDto> UpdateThemeSettingsAsync(Guid userId, UpdateThemeSettingsDto settings, string? updatedBy);
        
        // Update General settings
        Task<UserSettingsDto> UpdateGeneralSettingsAsync(Guid userId, UpdateGeneralSettingsDto settings, string? updatedBy);
        
        // Update Branding settings (Developer only)
        Task<BrandingSettingsDto> UpdateBrandingSettingsAsync(UpdateBrandingSettingsDto settings, string? updatedBy);
        
        // Reset Theme to defaults
        Task<UserSettingsDto> ResetThemeSettingsAsync(Guid userId, string? updatedBy);
        
        // Reset General to defaults
        Task<UserSettingsDto> ResetGeneralSettingsAsync(Guid userId, string? updatedBy);
        
        // Clear cache for a user
        Task ClearUserCacheAsync(Guid userId);
        
        // Clear branding cache
        Task ClearBrandingCacheAsync();
    }
}