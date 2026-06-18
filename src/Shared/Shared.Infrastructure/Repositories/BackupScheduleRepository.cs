// src/Shared/Shared.Infrastructure/Repositories/BackupScheduleRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class BackupScheduleRepository : IBackupScheduleRepository
    {
        private readonly AppDbContext _context;

        public BackupScheduleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BackupSchedule?> GetByIdAsync(long id)
        {
            return await _context.Set<BackupSchedule>()
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<BackupSchedule>> GetActiveSchedulesAsync()
        {
            return await _context.Set<BackupSchedule>()
                .Include(s => s.CreatedByUser)
                .Where(s => s.IsActive)
                .OrderBy(s => s.NextRunAt)
                .ToListAsync();
        }

        public async Task<BackupSchedule> AddAsync(BackupSchedule schedule)
        {
            await _context.Set<BackupSchedule>().AddAsync(schedule);
            return schedule;
        }

        public async Task UpdateAsync(BackupSchedule schedule)
        {
            schedule.UpdatedAt = DateTime.UtcNow;
            _context.Set<BackupSchedule>().Update(schedule);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(long id)
        {
            var schedule = await _context.Set<BackupSchedule>().FindAsync(id);
            if (schedule != null)
            {
                _context.Set<BackupSchedule>().Remove(schedule);
            }
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}