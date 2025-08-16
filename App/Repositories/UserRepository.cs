using Microsoft.EntityFrameworkCore;
using shop_back.App.Data;
using shop_back.App.Models;

namespace shop_back.App.Repositories
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
                .FirstOrDefaultAsync(u =>
                    u.Username == identifier ||
                    u.Email == identifier ||
                    u.MobileNo == identifier);
        }
    }
}
