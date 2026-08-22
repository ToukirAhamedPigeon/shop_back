// src/Shared/Shared.Infrastructure/Services/UserSettingService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using StackExchange.Redis;
using Microsoft.AspNetCore.Http;
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

            var theme = currentSettings.Theme;
            
            // Handle sidebar background image
            if (settings.SidebarBgFile != null)
            {
                if (!string.IsNullOrEmpty(theme.sidebar_bg_image))
                {
                    await FileHelper.DeleteFileAsync(theme.sidebar_bg_image);
                }
                
                var imagePath = await UploadThemeImageAsync(settings.SidebarBgFile, "themes/sidebar");
                if (!string.IsNullOrEmpty(imagePath))
                {
                    theme.sidebar_bg_image = imagePath;
                }
            }
            
            // Handle login background image
            if (settings.LoginBgFile != null)
            {
                if (!string.IsNullOrEmpty(theme.login_bg_image))
                {
                    await FileHelper.DeleteFileAsync(theme.login_bg_image);
                }
                
                var imagePath = await UploadThemeImageAsync(settings.LoginBgFile, "themes/login");
                if (!string.IsNullOrEmpty(imagePath))
                {
                    theme.login_bg_image = imagePath;
                }
            }
            
            // Handle other fields
            if (settings.primary_color != null) theme.primary_color = settings.primary_color;
            if (settings.secondary_color != null) theme.secondary_color = settings.secondary_color;
            if (settings.dark_mode.HasValue) theme.dark_mode = settings.dark_mode.Value;
            if (settings.custom_css != null) theme.custom_css = settings.custom_css;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = theme, General = currentSettings.General });
            userSetting.SettingsJson = updatedJson;

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
                actionType: "Update",
                detail: $"Theme settings updated for user {userId}",
                changes: JsonConvert.SerializeObject(new { before = currentSettings.Theme, after = theme }),
                modelName: "UserSetting",
                modelId: userId.ToString()
            );

            return await GetUserSettingsAsync(userId);
        }

        private async Task<string?> UploadThemeImageAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > 10 * 1024 * 1024) // 10MB limit
                throw new Exception("Image size must be less than 10MB");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP images are allowed");

            var resizeOptions = new ImageResizeOptions
            {
                Enabled = true,
                MaxWidth = 1920,
                MaxHeight = 1080,
                ResizeMode = ImageResizeMode.Max
            };

            return await FileHelper.SaveFileAsync(file, folder, true, resizeOptions);
        }

        public async Task<UserSettingsDto> UpdateGeneralSettingsAsync(Guid userId, UpdateGeneralSettingsDto settings, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            var general = currentSettings.General;
            if (settings.default_language != null) general.default_language = settings.default_language;
            if (settings.timezone != null) general.timezone = settings.timezone;
            if (settings.date_format != null) general.date_format = settings.date_format;
            if (settings.time_format != null) general.time_format = settings.time_format;
            if (settings.currency != null) general.currency = settings.currency;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = currentSettings.Theme, General = general });
            userSetting.SettingsJson = updatedJson;

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

            // Handle Logo upload
            if (settings.LogoFile != null)
            {
                if (!string.IsNullOrEmpty(currentSettings.logo))
                {
                    await FileHelper.DeleteFileAsync(currentSettings.logo);
                }
                
                var logoPath = await UploadBrandingImageAsync(settings.LogoFile, "branding");
                if (!string.IsNullOrEmpty(logoPath))
                {
                    currentSettings.logo = logoPath;
                }
            }

            // Handle Favicon upload
            if (settings.FaviconFile != null)
            {
                if (!string.IsNullOrEmpty(currentSettings.favicon))
                {
                    await FileHelper.DeleteFileAsync(currentSettings.favicon);
                }
                
                var faviconPath = await UploadBrandingImageAsync(settings.FaviconFile, "branding");
                if (!string.IsNullOrEmpty(faviconPath))
                {
                    currentSettings.favicon = faviconPath;
                }
            }

            // Handle other fields
            if (settings.app_name != null) currentSettings.app_name = settings.app_name;
            if (settings.footer_text != null) currentSettings.footer_text = settings.footer_text;

            branding.SettingsJson = JsonConvert.SerializeObject(currentSettings);

            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsedGuid))
            {
                updatedByGuid = parsedGuid;
                branding.UpdatedBy = updatedByGuid;
            }

            await _brandingSettingRepository.UpdateAsync(branding);
            await _brandingSettingRepository.SaveChangesAsync();

            await ClearBrandingCacheAsync();

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

        private async Task<string?> UploadBrandingImageAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > 5 * 1024 * 1024) // 5MB limit
                throw new Exception("File size must be less than 5MB");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/svg+xml" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP, and SVG images are allowed");

            var resizeOptions = new ImageResizeOptions
            {
                Enabled = true,
                MaxWidth = 500,
                MaxHeight = 500,
                ResizeMode = ImageResizeMode.Max
            };

            return await FileHelper.SaveFileAsync(file, folder, true, resizeOptions);
        }

        public async Task<UserSettingsDto> ResetThemeSettingsAsync(Guid userId, string? updatedBy)
        {
            var userSetting = await _userSettingRepository.GetOrCreateAsync(userId);
            var currentSettings = JsonConvert.DeserializeObject<UserSettingsDto>(userSetting.SettingsJson)!;

            currentSettings.Theme = _defaultTheme;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = _defaultTheme, General = currentSettings.General });
            userSetting.SettingsJson = updatedJson;

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

            currentSettings.General = _defaultGeneral;

            var updatedJson = JsonConvert.SerializeObject(new { Theme = currentSettings.Theme, General = _defaultGeneral });
            userSetting.SettingsJson = updatedJson;

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