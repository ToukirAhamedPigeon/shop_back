using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserLogRepository : IUserLogRepository
    {
        private readonly AppDbContext _context;

        public UserLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserLog> CreateAsync(UserLog log)
        {
            await _context.UserLogs.AddAsync(log);
            return log;
        }

        public async Task<UserLog?> GetByIdAsync(Guid id)
        {
            return await _context.UserLogs.FindAsync(id);
        }

        public async Task<IEnumerable<UserLog>> GetByUserIdAsync(Guid createdBy)
        {
            return await _context.UserLogs
                .Where(l => l.CreatedBy == createdBy)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserLog>> GetAllAsync()
        {
            return await _context.UserLogs
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
