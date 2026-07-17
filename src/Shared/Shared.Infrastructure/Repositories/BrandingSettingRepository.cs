// D:\shop\shop_back\src\Shared\Shared.Infrastructure\Repositories\BrandingSettingRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class BrandingSettingRepository : IBrandingSettingRepository
    {
        private readonly AppDbContext _context;

        public BrandingSettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BrandingSetting?> GetAsync()
        {
            return await _context.BrandingSettings
                .Include(bs => bs.UpdatedByUser)
                .FirstOrDefaultAsync();
        }

        public async Task<BrandingSetting> CreateAsync(BrandingSetting branding)
        {
            await _context.BrandingSettings.AddAsync(branding);
            return branding;
        }

        public async Task<BrandingSetting> UpdateAsync(BrandingSetting branding)
        {
            _context.BrandingSettings.Update(branding);
            return await Task.FromResult(branding);
        }

        public async Task<BrandingSetting> GetOrCreateAsync()
        {
            var branding = await GetAsync();
            
            if (branding == null)
            {
                var defaultSettings = new
                {
                    app_name = "Shop Management",
                    logo = "",
                    favicon = "",
                    footer_text = "© 2024 Shop Management. All rights reserved."
                };

                branding = new BrandingSetting
                {
                    SettingsJson = System.Text.Json.JsonSerializer.Serialize(defaultSettings)
                };

                await CreateAsync(branding);
                await SaveChangesAsync();
            }

            return branding;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}