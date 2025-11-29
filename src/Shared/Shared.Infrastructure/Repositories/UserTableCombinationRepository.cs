// src/Modules/UserTable/Infrastructure/Repositories/UserTableCombinationRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Application.Repositories;


namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserTableCombinationRepository : IUserTableCombinationRepository
    {
        private readonly AppDbContext _context;

        public UserTableCombinationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserTableCombination?> GetByTableIdAndUserId(string tableId, Guid userId)
        {
            return await _context.UserTableCombinations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TableId == tableId && x.UserId == userId);
        }

        public async Task AddAsync(UserTableCombination entity)
        {
            await _context.UserTableCombinations.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserTableCombination entity)
        {
            _context.UserTableCombinations.Update(entity);
            await _context.SaveChangesAsync();
        }
    }
}
