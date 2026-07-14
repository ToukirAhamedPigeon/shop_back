// D:\shop\shop_back\src\Shared\Shared.Infrastructure\Repositories\AppSettingRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class AppSettingRepository : IAppSettingRepository
    {
        private readonly AppDbContext _context;

        public AppSettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AppSetting>> GetAllAsync()
        {
            return await _context.AppSettings
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.SortOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<AppSetting>> GetByCategoryAsync(string category)
        {
            return await _context.AppSettings
                .Where(s => s.Category == category && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
        }

        public async Task<AppSetting?> GetByKeyAsync(string category, string key)
        {
            return await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Category == category && s.Key == key && s.IsActive);
        }

        public async Task<AppSetting?> GetByIdAsync(Guid id)
        {
            return await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<IEnumerable<string>> GetCategoriesAsync()
        {
            return await _context.AppSettings
                .Where(s => s.IsActive)
                .Select(s => s.Category)
                .Distinct()
                .ToListAsync();
        }

        public async Task<AppSetting> CreateAsync(AppSetting setting)
        {
            await _context.AppSettings.AddAsync(setting);
            return setting;
        }

        public void Update(AppSetting setting)
        {
            _context.AppSettings.Update(setting);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var setting = await GetByIdAsync(id);
            if (setting == null) return false;
            
            setting.IsActive = false;
            _context.AppSettings.Update(setting);
            return true;
        }

        public async Task<bool> ExistsAsync(string category, string key, Guid? excludeId = null)
        {
            var query = _context.AppSettings
                .Where(s => s.Category == category && s.Key == key && s.IsActive);
            
            if (excludeId.HasValue)
                query = query.Where(s => s.Id != excludeId.Value);
            
            return await query.AnyAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}