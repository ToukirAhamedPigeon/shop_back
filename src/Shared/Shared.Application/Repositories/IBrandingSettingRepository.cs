// D:\shop\shop_back\src\Shared\Shared.Application\Repositories\IBrandingSettingRepository.cs
using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IBrandingSettingRepository
    {
        /// <summary>
        /// Get branding settings (single row)
        /// </summary>
        Task<BrandingSetting?> GetAsync();
        
        /// <summary>
        /// Create new branding settings
        /// </summary>
        Task<BrandingSetting> CreateAsync(BrandingSetting branding);
        
        /// <summary>
        /// Update branding settings
        /// </summary>
        Task<BrandingSetting> UpdateAsync(BrandingSetting branding);
        
        /// <summary>
        /// Get or create branding settings
        /// </summary>
        Task<BrandingSetting> GetOrCreateAsync();
        
        /// <summary>
        /// Save changes to database
        /// </summary>
        Task SaveChangesAsync();
    }
}