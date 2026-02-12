using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class MailVerificationRepository : IMailVerificationRepository
    {
        private readonly AppDbContext _context;

        public MailVerificationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MailVerification?> GetByTokenAsync(string token)
        {
            return await _context.MailVerifications
                .Include(mv => mv.User) // Include user to update EmailVerifiedAt
                .FirstOrDefaultAsync(mv => mv.Token == token);
        }

        public async Task AddAsync(MailVerification entity)
        {
            await _context.MailVerifications.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<MailVerification?> GetLatestByUserIdAsync(Guid userId)
        {
            // 1️⃣ Try to get latest verification record
            var verification = await _context.MailVerifications
                .Where(mv => mv.UserId == userId)
                .OrderByDescending(mv => mv.CreatedAt)
                .Include(mv => mv.User)
                .FirstOrDefaultAsync();

            if (verification != null)
                return verification;

            // 2️⃣ If not found in MailVerification, check User table
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return null;

            // 3️⃣ Return empty MailVerification object with User populated
            return new MailVerification
            {
                UserId = user.Id,
                User = user,
                IsUsed = false,
                ExpiresAt = DateTime.MinValue
            };
        }

    }
}
