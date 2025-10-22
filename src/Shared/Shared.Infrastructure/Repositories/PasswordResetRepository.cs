using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class PasswordResetRepository : IPasswordResetRepository
    {
        private readonly AppDbContext _context;

        public PasswordResetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PasswordReset?> GetByTokenAsync(string token)
        {
            return await _context.PasswordResets
                .FirstOrDefaultAsync(p => p.Token == token && !p.Used);
        }

        public async Task<IEnumerable<PasswordReset>> GetAllByUserAsync(Guid userId)
        {
            return await _context.PasswordResets
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(PasswordReset passwordReset)
        {
            await _context.PasswordResets.AddAsync(passwordReset);
        }

        public Task UpdateAsync(PasswordReset passwordReset)
        {
            _context.PasswordResets.Update(passwordReset);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
