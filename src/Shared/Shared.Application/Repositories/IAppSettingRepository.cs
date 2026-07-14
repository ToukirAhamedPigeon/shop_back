// D:\shop\shop_back\src\Shared\Shared.Application\Repositories\IAppSettingRepository.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IAppSettingRepository
    {
        Task<IEnumerable<AppSetting>> GetAllAsync();
        Task<IEnumerable<AppSetting>> GetByCategoryAsync(string category);
        Task<AppSetting?> GetByKeyAsync(string category, string key);
        Task<AppSetting?> GetByIdAsync(Guid id);
        Task<IEnumerable<string>> GetCategoriesAsync();
        Task<AppSetting> CreateAsync(AppSetting setting);
        void Update(AppSetting setting);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ExistsAsync(string category, string key, Guid? excludeId = null);
        Task SaveChangesAsync();
    }
}