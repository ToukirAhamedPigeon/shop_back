// D:\shop\shop_back\src\Shared\Shared.Infrastructure\Repositories\UserSettingRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserSettingRepository : IUserSettingRepository
    {
        private readonly AppDbContext _context;

        public UserSettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserSetting?> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserSettings
                .Include(us => us.UpdatedByUser)
                .FirstOrDefaultAsync(us => us.UserId == userId);
        }

        public async Task<UserSetting> CreateAsync(UserSetting userSetting)
        {
            await _context.UserSettings.AddAsync(userSetting);
            return userSetting;
        }

        public async Task<UserSetting> UpdateAsync(UserSetting userSetting)
        {
            _context.UserSettings.Update(userSetting);
            return await Task.FromResult(userSetting);
        }

        public async Task<bool> ExistsAsync(Guid userId)
        {
            return await _context.UserSettings
                .AnyAsync(us => us.UserId == userId);
        }

        public async Task<UserSetting> GetOrCreateAsync(Guid userId)
        {
            var userSetting = await GetByUserIdAsync(userId);
            
            if (userSetting == null)
            {
                // Create default settings
                var defaultSettings = new
                {
                    Theme = new
                    {
                        primary_color = "#3B82F6",
                        secondary_color = "#10B981",
                        sidebar_bg_image = "",
                        login_bg_image = "",
                        dark_mode = false,
                        custom_css = ""
                    },
                    General = new
                    {
                        default_language = "en",
                        timezone = "Asia/Dhaka",
                        date_format = "DD/MM/YYYY",
                        time_format = "12h",
                        currency = "BDT"
                    }
                };

                userSetting = new UserSetting
                {
                    UserId = userId,
                    SettingsJson = System.Text.Json.JsonSerializer.Serialize(defaultSettings),
                    UpdatedBy = userId
                };

                await CreateAsync(userSetting);
                await SaveChangesAsync();
            }

            return userSetting;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}