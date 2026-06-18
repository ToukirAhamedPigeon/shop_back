// src/Shared/Shared.Infrastructure/Repositories/BackupRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.DTOs.Backups;
using shop_back.src.Shared.Infrastructure.Data;
using System.Linq.Dynamic.Core;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class BackupRepository : IBackupRepository
    {
        private readonly AppDbContext _context;

        public BackupRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Backup?> GetByIdAsync(long id)
        {
            return await _context.Set<Backup>()
                .Include(b => b.CreatedByUser)
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
        }

        public async Task<(IEnumerable<Backup> Items, int TotalCount, int GrandTotalCount)> GetFilteredAsync(BackupFilterRequest request)
        {
            IQueryable<Backup> query = _context.Set<Backup>()
                .Include(b => b.CreatedByUser)
                .Where(b => !b.IsDeleted);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.ToLower();
                query = query.Where(b =>
                    b.Name.ToLower().Contains(q) ||
                    b.FileName.ToLower().Contains(q) ||
                    b.StorageType.ToLower().Contains(q)
                );
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(b => b.Status == request.Status);
            }

            if (!string.IsNullOrWhiteSpace(request.StorageType))
            {
                query = query.Where(b => b.StorageType == request.StorageType);
            }

            if (request.FromDate.HasValue)
                query = query.Where(b => b.CreatedAt >= request.FromDate.Value);
            
            if (request.ToDate.HasValue)
                query = query.Where(b => b.CreatedAt <= request.ToDate.Value);

            // Get grand total count
            int grandTotalCount = await _context.Set<Backup>().CountAsync(b => !b.IsDeleted);

            // Apply sorting
            var sortBy = request.SortBy?.ToLower() ?? "createdat";
            var sortOrder = request.SortOrder?.ToLower() == "desc" ? "descending" : "ascending";
            query = query.OrderBy($"{sortBy} {sortOrder}");

            // Get total count after filters
            int totalCount = await query.CountAsync();

            // Apply pagination
            var items = await query
                .Skip((request.Page - 1) * request.Limit)
                .Take(request.Limit)
                .ToListAsync();

            return (items, totalCount, grandTotalCount);
        }

        public async Task<Backup> AddAsync(Backup backup)
        {
            await _context.Set<Backup>().AddAsync(backup);
            return backup;
        }

        public async Task UpdateAsync(Backup backup)
        {
            backup.UpdatedAt = DateTime.UtcNow;
            _context.Set<Backup>().Update(backup);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(long id)
        {
            var backup = await _context.Set<Backup>().FindAsync(id);
            if (backup != null)
            {
                backup.IsDeleted = true;
                backup.DeletedAt = DateTime.UtcNow;
                await UpdateAsync(backup);
            }
        }

        public async Task DeletePermanentlyAsync(long id)
        {
            var backup = await _context.Set<Backup>().FindAsync(id);
            if (backup != null)
            {
                _context.Set<Backup>().Remove(backup);
            }
            await Task.CompletedTask;
        }

        public async Task<List<Backup>> GetOldBackupsAsync(int retentionDays)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            return await _context.Set<Backup>()
                .Where(b => !b.IsDeleted && b.CreatedAt < cutoffDate)
                .ToListAsync();
        }

        public async Task<BackupStatisticsDto> GetStatisticsAsync()
        {
            var backups = await _context.Set<Backup>()
                .Where(b => !b.IsDeleted)
                .ToListAsync();

            var stats = new BackupStatisticsDto
            {
                TotalBackups = backups.Count,
                TotalSize = backups.Sum(b => b.FileSize),
                SuccessCount = backups.Count(b => b.Status == "Success"),
                FailedCount = backups.Count(b => b.Status == "Failed"),
                StorageUsed = new BackupStorageUsedDto
                {
                    Local = backups.Where(b => b.StorageType == "Local").Sum(b => b.FileSize),
                    RemoteServer = backups.Where(b => b.StorageType == "RemoteServer").Sum(b => b.FileSize),
                    GoogleDrive = backups.Where(b => b.StorageType == "GoogleDrive").Sum(b => b.FileSize)
                },
                LastBackupAt = backups.OrderByDescending(b => b.CreatedAt).FirstOrDefault()?.CreatedAt
            };

            return stats;
        }

        public async Task<bool> ExistsByFileNameAsync(string fileName)
        {
            return await _context.Set<Backup>()
                .AnyAsync(b => b.FileName == fileName && !b.IsDeleted);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}