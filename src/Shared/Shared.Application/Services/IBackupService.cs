// src/Shared/Shared.Application/Services/IBackupService.cs
using shop_back.src.Shared.Application.DTOs.Backups;

namespace shop_back.src.Shared.Application.Services
{
    public interface IBackupService
    {
        Task<BackupDto> CreateBackupAsync(CreateBackupRequest request, Guid? userId = null);
        Task<BackupDto?> GetBackupByIdAsync(long id);
        Task<(IEnumerable<BackupDto> Items, int TotalCount, int GrandTotalCount)> GetBackupsAsync(BackupFilterRequest request);
        Task DeleteBackupAsync(long id, Guid? userId = null);
        Task<string> DownloadBackupAsync(long id);
        Task RestoreBackupAsync(long id, Guid? userId = null);
        Task<BackupStatisticsDto> GetStatisticsAsync();
        Task CleanupOldBackupsAsync(int retentionDays, Guid? userId = null);
        
        // Schedule management
        Task<BackupScheduleDto> CreateScheduleAsync(BackupScheduleDto schedule, Guid? userId = null);
        Task<BackupScheduleDto> UpdateScheduleAsync(long id, BackupScheduleDto schedule, Guid? userId = null);
        Task DeleteScheduleAsync(long id, Guid? userId = null);
        Task<List<BackupScheduleDto>> GetSchedulesAsync();
        
        // Storage destination management
        Task<StorageDestinationDto> CreateStorageDestinationAsync(StorageDestinationDto destination, Guid? userId = null);
        Task<StorageDestinationDto> UpdateStorageDestinationAsync(long id, StorageDestinationDto destination, Guid? userId = null);
        Task DeleteStorageDestinationAsync(long id, Guid? userId = null);
        Task<List<StorageDestinationDto>> GetStorageDestinationsAsync();
        Task<bool> TestStorageConnectionAsync(long id);
        Task<DateTime?> GetNextScheduledBackupTimeAsync();
    }
}