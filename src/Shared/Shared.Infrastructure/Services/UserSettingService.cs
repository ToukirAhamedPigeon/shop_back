// D:\shop\shop_back\src\Shared\Shared.Infrastructure\Services\UserSettingService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using StackExchange.Redis;
using shop_back.src.Shared.Application.DTOs.Settings;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Helpers;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserSettingService : IUserSettingService
    {
        private readonly IUserSettingRepository _userSettingRepository;
        private readonly IBrandingSettingRepository _brandingSettingRepository;
        private readonly UserLogHelper _userLogHelper;
        private readonly IDatabase _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);
        private const string USER_CACHE_KEY_PREFIX = "UserSettings:";
        private const string BRANDING_CACHE_KEY = "BrandingSettings";

        private readonly ThemeSettingsDto _defaultTheme = new();
        private readonly GeneralSettingsDto _defaultGeneral = new();
        private readonly BrandingSettingsDto _defaultBranding = new();

        public UserSettingService(
            IUserSettingRepository userSettingRepository,
            IBrandingSettingRepository brandingSettingRepository,
            UserLogHelper userLogHelper,
            IConnectionMultiplexer redis)
        {
            _userSettingRepository = userSettingRepository;
            _brandingSettingRepository = brandingSettingRepository;
            _userLogHelper = userLogHelper;
            _redis = redis;
            _cache = redis.GetDatabase();
        }

        public async Task<UserSettingsDto> GetUserSettingsAsync(Guid userId)
        {
            var cacheKey = $"{USER_CACHE_KEY_PREFIX}{userId}";
            var cached = await _cache.StringGetAsync(cacheKey);

            if (cached.HasValue)
            {
                return JsonConvert.DeserializeObject<UserSettingsDto>(cached!)!;
            }

            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            
            var settings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;
            settings.UpdatedAt = userSetting.UpdatedAt;
            settings.UpdatedBy = userSetting.UpdatedByUser?.Name;

            await _cache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(settings), _cacheTtl);

            return settings;
        }

        public async Task<BrandingSettingsDto> GetBrandingSettingsAsync()
        {
            var cached = await _cache.StringGetAsync(BRANDING_CACHE_KEY);

            if (cached.HasValue)
            {
                return JsonConvert.DeserializeObject<BrandingSettingsDto>(cached!)!;
            }

            var branding = await _brandingSettingRepository.GetOrCreateAsync();
            var settings = JsonConvert.DeserializeObject<BrandingSettingsDto>(branding.SettingsJson)!;

            await _cache.StringSetAsync(BRANDING_CACHE_KEY, JsonConvert.SerializeObject(settings), _cacheTtl);

            return settings;
        }

        public async Task<SettingsResponseDto> GetAllSettingsAsync(Guid userId)
        {
            var userSettings = await GetUserSettingsAsync(userId);
            var brandingSettings = await GetBrandingSettingsAsync();

            // BrandingSettingsDto doesn't have UpdatedAt/UpdatedBy, so we use user's values
            return new SettingsResponseDto
            {
                User = userSettings,
                Branding = brandingSettings,
                LastUpdated = userSettings.UpdatedAt,
                UpdatedBy = userSettings.UpdatedBy
            };
        }

        public async Task<UserSettingsDto> UpdateThemeSettingsAsync(Guid userId, UpdateThemeSettingsDto settings, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            // Update only provided fields
            var theme = currentSettings.Theme;
            if (settings.primary_color != null) theme.primary_color = settings.primary_color;
            if (settings.secondary_color != null) theme.secondary_color = settings.secondary_color;
            if (settings.sidebar_bg_image != null) theme.sidebar_bg_image = settings.sidebar_bg_image;
            if (settings.login_bg_image != null) theme.login_bg_image = settings.login_bg_image;
            if (settings.dark_mode.HasValue) theme.dark_mode = settings.dark_mode.Value;
            if (settings.custom_css != null) theme.custom_css = settings.custom_css;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = theme, General = currentSettings.General });
            userSetting.SettingsJson = updatedJson;

            // Parse updatedBy to Guid
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                userSetting.UpdatedBy = updatedByGuid;
            }

            await _userSettingRepository.UpdateAsync(userSetting);
            await _userSettingRepository.SaveChangesAsync();

            // Clear cache
            await ClearUserCacheAsync(userId);

            // Log changes
            await _userLogHelper.LogAsync(
                userId: updatedByGuid ?? userId,
                actionType: "Update",
                detail: $"Theme settings updated for user {userId}",
                changes: JsonConvert.SerializeObject(new { before = currentSettings.Theme, after = theme }),
                modelName: "UserSetting",
                modelId: userId.ToString()
            );

            return await GetUserSettingsAsync(userId);
        }

        public async Task<UserSettingsDto> UpdateGeneralSettingsAsync(Guid userId, UpdateGeneralSettingsDto settings, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            // Update only provided fields
            var general = currentSettings.General;
            if (settings.default_language != null) general.default_language = settings.default_language;
            if (settings.timezone != null) general.timezone = settings.timezone;
            if (settings.date_format != null) general.date_format = settings.date_format;
            if (settings.time_format != null) general.time_format = settings.time_format;
            if (settings.currency != null) general.currency = settings.currency;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = currentSettings.Theme, General = general });
            userSetting.SettingsJson = updatedJson;

            // Parse updatedBy to Guid
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                userSetting.UpdatedBy = updatedByGuid;
            }

            await _userSettingRepository.UpdateAsync(userSetting);
            await _userSettingRepository.SaveChangesAsync();

            // Clear cache
            await ClearUserCacheAsync(userId);

            // Log changes
            await _userLogHelper.LogAsync(
                userId: updatedByGuid ?? userId,
                actionType: "Update",
                detail: $"General settings updated for user {userId}",
                changes: JsonConvert.SerializeObject(new { before = currentSettings.General, after = general }),
                modelName: "UserSetting",
                modelId: userId.ToString()
            );

            return await GetUserSettingsAsync(userId);
        }

        public async Task<BrandingSettingsDto> UpdateBrandingSettingsAsync(UpdateBrandingSettingsDto settings, string? updatedBy)
        {
            var branding = await _brandingSettingRepository.GetOrCreateAsync();
            var currentSettings = JsonConvert.DeserializeObject<BrandingSettingsDto>(branding.SettingsJson)!;

            // Update only provided fields
            if (settings.app_name != null) currentSettings.app_name = settings.app_name;
            if (settings.logo != null) currentSettings.logo = settings.logo;
            if (settings.favicon != null) currentSettings.favicon = settings.favicon;
            if (settings.footer_text != null) currentSettings.footer_text = settings.footer_text;

            branding.SettingsJson = JsonConvert.SerializeObject(currentSettings);

            // Parse updatedBy to Guid
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                branding.UpdatedBy = updatedByGuid;
            }

            await _brandingSettingRepository.UpdateAsync(branding);
            await _brandingSettingRepository.SaveChangesAsync();

            // Clear cache
            await ClearBrandingCacheAsync();

            // Log changes
            await _userLogHelper.LogAsync(
                userId: updatedByGuid ?? Guid.Empty,
                actionType: "Update",
                detail: "Branding settings updated",
                changes: JsonConvert.SerializeObject(new { before = currentSettings, after = settings }),
                modelName: "BrandingSetting",
                modelId: branding.Id.ToString()
            );

            return await GetBrandingSettingsAsync();
        }

        public async Task<UserSettingsDto> ResetThemeSettingsAsync(Guid userId, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            // Reset to defaults
            currentSettings.Theme = _defaultTheme;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = _defaultTheme, General = currentSettings.General });
            userSetting.SettingsJson = updatedJson;

            // Parse updatedBy to Guid
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                userSetting.UpdatedBy = updatedByGuid;
            }

            await _userSettingRepository.UpdateAsync(userSetting);
            await _userSettingRepository.SaveChangesAsync();
            await ClearUserCacheAsync(userId);

            await _userLogHelper.LogAsync(
                userId: updatedByGuid ?? userId,
                actionType: "Reset",
                detail: $"Theme settings reset to defaults for user {userId}",
                modelName: "UserSetting",
                modelId: userId.ToString()
            );

            return await GetUserSettingsAsync(userId);
        }

        public async Task<UserSettingsDto> ResetGeneralSettingsAsync(Guid userId, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            // Reset to defaults
            currentSettings.General = _defaultGeneral;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = currentSettings.Theme, General = _defaultGeneral });
            userSetting.SettingsJson = updatedJson;

            // Parse updatedBy to Guid
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                userSetting.UpdatedBy = updatedByGuid;
            }

            await _userSettingRepository.UpdateAsync(userSetting);
            await _userSettingRepository.SaveChangesAsync();
            await ClearUserCacheAsync(userId);

            await _userLogHelper.LogAsync(
                userId: updatedByGuid ?? userId,
                actionType: "Reset",
                detail: $"General settings reset to defaults for user {userId}",
                modelName: "UserSetting",
                modelId: userId.ToString()
            );

            return await GetUserSettingsAsync(userId);
        }

        public async Task ClearUserCacheAsync(Guid userId)
        {
            var cacheKey = $"{USER_CACHE_KEY_PREFIX}{userId}";
            await _cache.KeyDeleteAsync(cacheKey);
        }

        public async Task ClearBrandingCacheAsync()
        {
            await _cache.KeyDeleteAsync(BRANDING_CACHE_KEY);
        }
    }
}