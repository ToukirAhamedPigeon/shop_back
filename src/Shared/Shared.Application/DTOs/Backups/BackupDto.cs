// src/Shared/Shared.Application/DTOs/Backups/BackupDto.cs
namespace shop_back.src.Shared.Application.DTOs.Backups
{
    public class BackupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageType { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string? Checksum { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateBackupRequest
    {
        public string? Name { get; set; }
        public List<string>? StorageDestinations { get; set; }
        public bool IsManual { get; set; } = true; // true for manual, false for auto
    }

    public class BackupFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        public string? Status { get; set; }
        public string? StorageType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class BackupStatisticsDto
    {
        public int TotalBackups { get; set; }
        public long TotalSize { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public BackupStorageUsedDto StorageUsed { get; set; } = new();
        public DateTime? LastBackupAt { get; set; }
        public DateTime? NextBackupAt { get; set; }
    }

    public class BackupStorageUsedDto
    {
        public long Local { get; set; }
        public long RemoteServer { get; set; }
        public long GoogleDrive { get; set; }
    }

    public class BackupScheduleDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;  // auto-generated
        public int IntervalValue { get; set; } = 1;
        public string IntervalUnit { get; set; } = "days";
        public bool IsActive { get; set; }
        public int RetentionDays { get; set; }
        public List<string> StorageDestinations { get; set; } = new();
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime? CurrentTime { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StorageDestinationDto
    {
        public long Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Config { get; set; } = new();
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public bool IsPrimary { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BackupCleanupRequest
    {
        public int RetentionDays { get; set; } = 7;
    }
}