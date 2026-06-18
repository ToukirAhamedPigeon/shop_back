// src/Shared/Shared.Infrastructure/Repositories/StorageDestinationRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class StorageDestinationRepository : IStorageDestinationRepository
    {
        private readonly AppDbContext _context;

        public StorageDestinationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StorageDestination?> GetByIdAsync(long id)
        {
            return await _context.Set<StorageDestination>()
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<StorageDestination>> GetActiveDestinationsAsync()
        {
            return await _context.Set<StorageDestination>()
                .Include(s => s.CreatedByUser)
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.IsPrimary)
                .ThenBy(s => s.Priority)
                .ToListAsync();
        }

        public async Task<StorageDestination?> GetPrimaryDestinationAsync()
        {
            return await _context.Set<StorageDestination>()
                .FirstOrDefaultAsync(s => s.IsPrimary && s.IsActive);
        }

        public async Task<StorageDestination> AddAsync(StorageDestination destination)
        {
            await _context.Set<StorageDestination>().AddAsync(destination);
            return destination;
        }

        public async Task UpdateAsync(StorageDestination destination)
        {
            destination.UpdatedAt = DateTime.UtcNow;
            _context.Set<StorageDestination>().Update(destination);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(long id)
        {
            var destination = await _context.Set<StorageDestination>().FindAsync(id);
            if (destination != null)
            {
                _context.Set<StorageDestination>().Remove(destination);
            }
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}