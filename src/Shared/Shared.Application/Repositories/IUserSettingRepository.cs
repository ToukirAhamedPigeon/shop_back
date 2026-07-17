// D:\shop\shop_back\src\Shared\Shared.Application\Repositories\IUserSettingRepository.cs
using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserSettingRepository
    {
        /// <summary>
        /// Get user settings by user ID
        /// </summary>
        Task<UserSetting?> GetByUserIdAsync(Guid userId);
        
        /// <summary>
        /// Create new user settings
        /// </summary>
        Task<UserSetting> CreateAsync(UserSetting userSetting);
        
        /// <summary>
        /// Update existing user settings
        /// </summary>
        Task<UserSetting> UpdateAsync(UserSetting userSetting);
        
        /// <summary>
        /// Check if user settings exist
        /// </summary>
        Task<bool> ExistsAsync(Guid userId);
        
        /// <summary>
        /// Get or create user settings (upsert pattern)
        /// </summary>
        Task<UserSetting> GetOrCreateAsync(Guid userId);
        
        /// <summary>
        /// Save changes to database
        /// </summary>
        Task SaveChangesAsync();
    }
}