// D:\shop\shop_back\src\Shared\Shared.Application\Services\IAppSettingService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Application.DTOs.Settings;

namespace shop_back.src.Shared.Application.Services
{
    public interface IAppSettingService
    {
        Task<SettingsResponseDto> GetAllSettingsAsync();
        Task<IEnumerable<SettingsGroupDto>> GetGroupedSettingsAsync();
        Task<IEnumerable<AppSettingDto>> GetSettingsByCategoryAsync(string category);
        Task<AppSettingDto?> GetSettingAsync(string category, string key);
        Task<T?> GetSettingValueAsync<T>(string category, string key, T? defaultValue = default);
        Task<AppSettingDto> UpdateSettingAsync(Guid id, string value, string? updatedBy);
        Task<bool> UpdateCategorySettingsAsync(string category, Dictionary<string, object> settings, string? updatedBy);
        Task<bool> ResetCategoryToDefaultAsync(string category, string? updatedBy);
        Task<IEnumerable<AppSettingDto>> GetPublicSettingsAsync();
        Task<bool> ClearCacheAsync();
    }
}