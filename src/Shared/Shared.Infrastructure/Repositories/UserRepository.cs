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
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .FirstOrDefaultAsync(u =>
                    u.Username == identifier ||
                    u.Email == identifier ||
                    u.MobileNo == identifier);
        }
    }
}
