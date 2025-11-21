using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdentifierAsync(string identifier)
        {
            // ✅ Correction:
            // Merged the `Where(u => !u.IsDeleted)` and `FirstOrDefaultAsync(...)`
            // into a single predicate so EF Core can fully translate to SQL
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    (u.Username == identifier ||
                     u.Email == identifier ||
                     u.MobileNo == identifier));
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.Email == email);
        }

         public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.Id == id);
        }

        public async Task<User?> GetByMobileNoAsync(string mobileNo)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.MobileNo == mobileNo);
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
