using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class OtpRepository : IOtpRepository
    {
        private readonly AppDbContext _context;

        public OtpRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Otp?> GetByEmailAndPurposeAsync(string email, string purpose)
        {
            return await _context.Otps
                .Where(o => o.Email == email && o.Purpose == purpose && !o.Used)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Otp?> GetByCodeHashAsync(string email, string codeHash)
        {
            return await _context.Otps
                .FirstOrDefaultAsync(o => o.Email == email && o.CodeHash == codeHash && !o.Used);
        }

        public async Task<IEnumerable<Otp>> GetAllByUserAsync(Guid userId)
        {
            return await _context.Otps
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Otp otp)
        {
            await _context.Otps.AddAsync(otp);
        }

        public Task UpdateAsync(Otp otp)
        {
            _context.Otps.Update(otp);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
