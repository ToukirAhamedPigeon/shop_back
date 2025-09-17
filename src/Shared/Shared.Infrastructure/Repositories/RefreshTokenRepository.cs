using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(r => r.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked);
        }

        public async Task AddAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task RevokeAsync(RefreshToken refreshToken)
        {
            refreshToken.IsRevoked = true;
            refreshToken.UpdatedAt = DateTime.UtcNow;
            refreshToken.UpdatedBy = refreshToken.UserId;

            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync();

            if (tokens.Count == 0) return;

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.UpdatedAt = DateTime.UtcNow;
                token.UpdatedBy = userId;
            }

            _context.RefreshTokens.UpdateRange(tokens);
            await _context.SaveChangesAsync();
        }

        public async Task RevokeOtherAsync(string exceptRefreshToken, Guid userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked && r.Token != exceptRefreshToken)
                .ToListAsync();

            if (tokens.Count == 0) return;

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.UpdatedAt = DateTime.UtcNow;
                token.UpdatedBy = userId;
            }

            _context.RefreshTokens.UpdateRange(tokens);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveExpiredAsync()
        {
            var cutoff = DateTime.UtcNow; // strictly expired tokens

            var expiredTokens = await _context.RefreshTokens
                .Where(r => r.ExpiresAt < cutoff)
                .ToListAsync();

            if (expiredTokens.Count == 0) return;

            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
