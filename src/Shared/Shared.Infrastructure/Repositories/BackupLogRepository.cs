// src/Shared/Shared.Infrastructure/Repositories/BackupLogRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class BackupLogRepository : IBackupLogRepository
    {
        private readonly AppDbContext _context;

        public BackupLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BackupLog> AddAsync(BackupLog log)
        {
            await _context.Set<BackupLog>().AddAsync(log);
            return log;
        }

        public async Task<List<BackupLog>> GetByBackupIdAsync(long backupId)
        {
            return await _context.Set<BackupLog>()
                .Where(l => l.BackupId == backupId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}