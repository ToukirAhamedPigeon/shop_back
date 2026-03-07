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

        public async Task<PasswordReset?> GetByTokenAsync(string token, string tokenType = "reset")
        {
            return await _context.PasswordResets
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Token == token && 
                                        !p.Used && 
                                        p.TokenType == tokenType);
        }

        public async Task<IEnumerable<PasswordReset>> GetAllByUserAsync(Guid userId, string tokenType)
        {
            return await _context.PasswordResets
                .Where(p => p.UserId == userId && p.TokenType == tokenType)
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

        public async Task MarkExistingTokensAsUsedAsync(Guid userId, string tokenType)
        {
            var tokens = await _context.PasswordResets
                .Where(p => p.UserId == userId && 
                            p.TokenType == tokenType && 
                            !p.Used)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.Used = true;
            }
        }
    }
}
