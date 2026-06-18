// src/Shared/Shared.Application/Repositories/IBackupRepository.cs
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Backups;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IBackupRepository
    {
        Task<Backup?> GetByIdAsync(long id);
        Task<(IEnumerable<Backup> Items, int TotalCount, int GrandTotalCount)> GetFilteredAsync(BackupFilterRequest request);
        Task<Backup> AddAsync(Backup backup);
        Task UpdateAsync(Backup backup);
        Task DeleteAsync(long id);
        Task DeletePermanentlyAsync(long id);
        Task<List<Backup>> GetOldBackupsAsync(int retentionDays);
        Task<BackupStatisticsDto> GetStatisticsAsync();
        Task SaveChangesAsync();
        Task<bool> ExistsByFileNameAsync(string fileName);
    }

    public interface IBackupScheduleRepository
    {
        Task<BackupSchedule?> GetByIdAsync(long id);
        Task<List<BackupSchedule>> GetActiveSchedulesAsync();
        Task<BackupSchedule> AddAsync(BackupSchedule schedule);
        Task UpdateAsync(BackupSchedule schedule);
        Task DeleteAsync(long id);
        Task SaveChangesAsync();
    }

    public interface IStorageDestinationRepository
    {
        Task<StorageDestination?> GetByIdAsync(long id);
        Task<List<StorageDestination>> GetActiveDestinationsAsync();
        Task<StorageDestination?> GetPrimaryDestinationAsync();
        Task<StorageDestination> AddAsync(StorageDestination destination);
        Task UpdateAsync(StorageDestination destination);
        Task DeleteAsync(long id);
        Task SaveChangesAsync();
    }

    public interface IBackupLogRepository
    {
        Task<BackupLog> AddAsync(BackupLog log);
        Task<List<BackupLog>> GetByBackupIdAsync(long backupId);
        Task SaveChangesAsync();
    }
}