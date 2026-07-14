// D:\shop\shop_back\src\Shared\Shared.Infrastructure\Services\AppSettingService.cs
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
    public class AppSettingService : IAppSettingService
    {
        private readonly IAppSettingRepository _repository;
        private readonly AppDbContext _context;
        private readonly UserLogHelper _userLogHelper;
        private readonly IDatabase _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);
        private const string CACHE_KEY_PREFIX = "AppSettings:";

        // Category display names and icons
        private static readonly Dictionary<string, (string DisplayName, string Icon)> CategoryMetadata = new()
        {
            ["Theme"] = ("Theme & Appearance", "PaintBucket"),
            ["Branding"] = ("Branding", "Tag"),
            ["General"] = ("General", "Settings")
        };

        public AppSettingService(
            IAppSettingRepository repository,
            AppDbContext context,
            UserLogHelper userLogHelper,
            IConnectionMultiplexer redis)
        {
            _repository = repository;
            _context = context;
            _userLogHelper = userLogHelper;
            _redis = redis;
            _cache = redis.GetDatabase();
        }

        public async Task<SettingsResponseDto> GetAllSettingsAsync()
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}all";
            var cached = await _cache.StringGetAsync(cacheKey);
            
            if (cached.HasValue)
            {
                return JsonConvert.DeserializeObject<SettingsResponseDto>(cached!)!;
            }

            var settings = await _repository.GetAllAsync();
            var groups = await BuildGroupsAsync(settings);
            
            var response = new SettingsResponseDto
            {
                Groups = groups.ToList(),
                LastUpdated = settings.Max(s => s.UpdatedAt),
                UpdatedBy = settings
                    .Where(s => s.UpdatedBy.HasValue)
                    .OrderByDescending(s => s.UpdatedAt)
                    .FirstOrDefault()?.UpdatedByUser?.Name
            };

            await _cache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(response), _cacheTtl);
            
            return response;
        }

        public async Task<IEnumerable<SettingsGroupDto>> GetGroupedSettingsAsync()
        {
            var settings = await _repository.GetAllAsync();
            return await BuildGroupsAsync(settings);
        }

        public async Task<IEnumerable<AppSettingDto>> GetSettingsByCategoryAsync(string category)
        {
            var settings = await _repository.GetByCategoryAsync(category);
            return settings.Select(MapToDto);
        }

        public async Task<AppSettingDto?> GetSettingAsync(string category, string key)
        {
            var setting = await _repository.GetByKeyAsync(category, key);
            return setting == null ? null : MapToDto(setting);
        }

        public async Task<T?> GetSettingValueAsync<T>(string category, string key, T? defaultValue = default)
        {
            var setting = await _repository.GetByKeyAsync(category, key);
            if (setting == null || string.IsNullOrEmpty(setting.Value))
                return defaultValue;

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)bool.Parse(setting.Value);
                }
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)int.Parse(setting.Value);
                }
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)setting.Value;
                }
                return JsonConvert.DeserializeObject<T>(setting.Value) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task<AppSettingDto> UpdateSettingAsync(Guid id, string value, string? updatedBy)
        {
            var setting = await _repository.GetByIdAsync(id);
            if (setting == null)
                throw new Exception("Setting not found");

            var beforeValue = setting.Value;
            
            // Validate based on data type
            if (!ValidateValue(setting.DataType, value, out var error))
                throw new Exception(error ?? "Invalid value for this setting type");

            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var userId))
            {
                setting.UpdatedBy = userId;
            }

            _repository.Update(setting);
            await _repository.SaveChangesAsync();

            // Log change
            await _userLogHelper.LogAsync(
                userId: setting.UpdatedBy ?? Guid.Empty,
                actionType: "Update",
                detail: $"Setting '{setting.Category}.{setting.Key}' updated",
                changes: JsonConvert.SerializeObject(new { before = beforeValue, after = value }),
                modelName: "AppSetting",
                modelId: setting.Id.ToString()
            );

            // Clear cache
            await ClearCacheAsync();

            return MapToDto(setting);
        }

        public async Task<bool> UpdateCategorySettingsAsync(string category, Dictionary<string, object> settings, string? updatedBy)
        {
            var existingSettings = await _repository.GetByCategoryAsync(category);
            var updatedCount = 0;

            foreach (var kvp in settings)
            {
                var setting = existingSettings.FirstOrDefault(s => s.Key == kvp.Key);
                if (setting == null) continue;

                var stringValue = kvp.Value?.ToString() ?? string.Empty;
                
                if (!ValidateValue(setting.DataType, stringValue, out var error))
                    throw new Exception($"Invalid value for '{setting.Key}': {error}");

                setting.Value = stringValue;
                setting.UpdatedAt = DateTime.UtcNow;
                
                if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var userId))
                {
                    setting.UpdatedBy = userId;
                }

                _repository.Update(setting);
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await _repository.SaveChangesAsync();
                await ClearCacheAsync();

                await _userLogHelper.LogAsync(
                    userId: Guid.TryParse(updatedBy, out var uid) ? uid : Guid.Empty,
                    actionType: "BatchUpdate",
                    detail: $"Updated {updatedCount} settings in category '{category}'",
                    modelName: "AppSetting",
                    modelId: category
                );
            }

            return updatedCount > 0;
        }

        public async Task<bool> ResetCategoryToDefaultAsync(string category, string? updatedBy)
        {
            var defaultSettings = GetDefaultSettings();
            var categoryDefaults = defaultSettings.Where(s => s.Category == category).ToList();

            if (!categoryDefaults.Any())
                return false;

            var existingSettings = await _repository.GetByCategoryAsync(category);
            var updatedCount = 0;

            foreach (var defaultSetting in categoryDefaults)
            {
                var existing = existingSettings.FirstOrDefault(s => s.Key == defaultSetting.Key);
                if (existing == null) continue;

                existing.Value = defaultSetting.Value;
                existing.UpdatedAt = DateTime.UtcNow;
                
                if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var userId))
                {
                    existing.UpdatedBy = userId;
                }

                _repository.Update(existing);
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await _repository.SaveChangesAsync();
                await ClearCacheAsync();

                await _userLogHelper.LogAsync(
                    userId: Guid.TryParse(updatedBy, out var uid) ? uid : Guid.Empty,
                    actionType: "Reset",
                    detail: $"Reset {updatedCount} settings in category '{category}' to defaults",
                    modelName: "AppSetting",
                    modelId: category
                );
            }

            return updatedCount > 0;
        }

        public async Task<IEnumerable<AppSettingDto>> GetPublicSettingsAsync()
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}public";
            var cached = await _cache.StringGetAsync(cacheKey);
            
            if (cached.HasValue)
            {
                return JsonConvert.DeserializeObject<IEnumerable<AppSettingDto>>(cached!)!;
            }

            // Only return Theme and Branding settings (non-sensitive)
            var categories = new[] { "Theme", "Branding" };
            var settings = await _context.AppSettings
                .Where(s => categories.Contains(s.Category) && s.IsActive)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.SortOrder)
                .ToListAsync();

            var result = settings.Select(MapToDto).ToList();
            await _cache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(result), _cacheTtl);
            
            return result;
        }

        // ✅ FIXED: Removed 'async' keyword since no await operators are used
        public Task<bool> ClearCacheAsync()
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    var keys = server.Keys(pattern: $"{CACHE_KEY_PREFIX}*").ToList();
                    if (keys.Any())
                    {
                        // Use synchronous delete since we're in a synchronous context
                        foreach (var key in keys)
                        {
                            _cache.KeyDelete(key);
                        }
                    }
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing settings cache: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        // ✅ Alternative: If you prefer async with Task.Run
        // public async Task<bool> ClearCacheAsync()
        // {
        //     return await Task.Run(() =>
        //     {
        //         try
        //         {
        //             var endpoints = _redis.GetEndPoints();
        //             foreach (var endpoint in endpoints)
        //             {
        //                 var server = _redis.GetServer(endpoint);
        //                 var keys = server.Keys(pattern: $"{CACHE_KEY_PREFIX}*").ToList();
        //                 if (keys.Any())
        //                 {
        //                     foreach (var key in keys)
        //                     {
        //                         _cache.KeyDelete(key);
        //                     }
        //                 }
        //             }
        //             return true;
        //         }
        //         catch
        //         {
        //             return false;
        //         }
        //     });
        // }

        #region Private Methods

        private async Task<IEnumerable<SettingsGroupDto>> BuildGroupsAsync(IEnumerable<AppSetting> settings)
        {
            var groups = settings
                .GroupBy(s => s.Category)
                .Select(g => new SettingsGroupDto
                {
                    Category = g.Key,
                    DisplayName = CategoryMetadata.TryGetValue(g.Key, out var meta) ? meta.DisplayName : g.Key,
                    Icon = CategoryMetadata.TryGetValue(g.Key, out var metaIcon) ? metaIcon.Icon : "Settings",
                    SortOrder = g.Key == "Theme" ? 1 : g.Key == "Branding" ? 2 : 3,
                    Settings = g.OrderBy(s => s.SortOrder).Select(MapToDto).ToList()
                })
                .OrderBy(g => g.SortOrder);

            return await Task.FromResult(groups);
        }

        private AppSettingDto MapToDto(AppSetting setting)
        {
            var displayName = setting.Key
                .Replace("_", " ")
                .ToLower()
                .Split(' ')
                .Select(word => char.ToUpper(word[0]) + word.Substring(1))
                .Aggregate((a, b) => $"{a} {b}");

            return new AppSettingDto
            {
                Id = setting.Id,
                Category = setting.Category,
                Key = setting.Key,
                Value = setting.Value,
                DataType = setting.DataType,
                Description = setting.Description,
                IsEncrypted = setting.IsEncrypted,
                IsActive = setting.IsActive,
                SortOrder = setting.SortOrder,
                DisplayName = displayName,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt,
                CreatedByName = setting.CreatedByUser?.Name,
                UpdatedByName = setting.UpdatedByUser?.Name
            };
        }

        private bool ValidateValue(string dataType, string value, out string? error)
        {
            error = null;

            if (string.IsNullOrEmpty(value))
                return true;

            try
            {
                switch (dataType.ToLower())
                {
                    case "boolean":
                        if (!bool.TryParse(value, out _))
                        {
                            error = "Must be 'true' or 'false'";
                            return false;
                        }
                        break;
                    case "number":
                        if (!int.TryParse(value, out _))
                        {
                            error = "Must be a valid number";
                            return false;
                        }
                        break;
                    case "color":
                        if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$"))
                        {
                            error = "Must be a valid hex color (e.g., #3B82F6)";
                            return false;
                        }
                        break;
                    case "email":
                        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                        {
                            error = "Must be a valid email address";
                            return false;
                        }
                        break;
                }
                return true;
            }
            catch
            {
                error = "Invalid value";
                return false;
            }
        }

        private List<AppSetting> GetDefaultSettings()
        {
            return new List<AppSetting>
            {
                // Theme
                new() { Category = "Theme", Key = "primary_color", Value = "#3B82F6", DataType = "color", Description = "Primary color for buttons, links, and headers", SortOrder = 1 },
                new() { Category = "Theme", Key = "secondary_color", Value = "#10B981", DataType = "color", Description = "Secondary color for cards, borders, and badges", SortOrder = 2 },
                new() { Category = "Theme", Key = "sidebar_bg_image", Value = "", DataType = "image", Description = "Background image for sidebar", SortOrder = 3 },
                new() { Category = "Theme", Key = "login_bg_image", Value = "", DataType = "image", Description = "Background image for login page", SortOrder = 4 },
                new() { Category = "Theme", Key = "dark_mode", Value = "false", DataType = "boolean", Description = "Enable dark mode globally", SortOrder = 5 },
                new() { Category = "Theme", Key = "custom_css", Value = "", DataType = "textarea", Description = "Custom CSS to override default styles", SortOrder = 6 },
                
                // Branding
                new() { Category = "Branding", Key = "app_name", Value = "Shop Management", DataType = "text", Description = "Application name displayed throughout the app", SortOrder = 1 },
                new() { Category = "Branding", Key = "logo", Value = "", DataType = "image", Description = "Application logo", SortOrder = 2 },
                new() { Category = "Branding", Key = "favicon", Value = "", DataType = "image", Description = "Browser favicon", SortOrder = 3 },
                new() { Category = "Branding", Key = "footer_text", Value = "© 2024 Shop Management. All rights reserved.", DataType = "text", Description = "Footer copyright text", SortOrder = 4 },
                
                // General
                new() { Category = "General", Key = "default_language", Value = "en", DataType = "select", Description = "Default application language", SortOrder = 1 },
                new() { Category = "General", Key = "timezone", Value = "Asia/Dhaka", DataType = "select", Description = "Default timezone", SortOrder = 2 },
                new() { Category = "General", Key = "date_format", Value = "DD/MM/YYYY", DataType = "select", Description = "Date display format", SortOrder = 3 },
                new() { Category = "General", Key = "time_format", Value = "12h", DataType = "select", Description = "Time display format (12h or 24h)", SortOrder = 4 },
                new() { Category = "General", Key = "currency", Value = "BDT", DataType = "select", Description = "Default currency", SortOrder = 5 }
            };
        }

        #endregion
    }
}