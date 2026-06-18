// src/Shared/Shared.Domain/Entities/Backup.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("backups")]
    public class Backup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Column("file_size")]
        public long FileSize { get; set; }

        [Required]
        [Column("storage_type")]
        public string StorageType { get; set; } = string.Empty; // Local, RemoteServer, GoogleDrive

        [Required]
        [Column("storage_path")]
        public string StoragePath { get; set; } = string.Empty;

        [Column("checksum")]
        public string? Checksum { get; set; }

        [Required]
        [Column("status")]
        public string Status { get; set; } = "InProgress"; // Success, Failed, InProgress

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CreatedBy))]
        public User? CreatedByUser { get; set; }
    }

    [Table("backup_schedules")]
    public class BackupSchedule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("cron_expression")]
        public string CronExpression { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("retention_days")]
        public int RetentionDays { get; set; } = 7;

        [Column("storage_destinations")]
        public List<string> StorageDestinations { get; set; } = new();

        [Column("last_run_at")]
        public DateTime? LastRunAt { get; set; }

        [Column("next_run_at")]
        public DateTime? NextRunAt { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CreatedBy))]
        public User? CreatedByUser { get; set; }
    }

    [Table("storage_destinations")]
    public class StorageDestination
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("config")]
        public string ConfigJson { get; set; } = "{}";

        [NotMapped]
        public Dictionary<string, object> Config
        {
            get => string.IsNullOrEmpty(ConfigJson) ? new Dictionary<string, object>() : 
                   System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(ConfigJson) ?? new();
            set => ConfigJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [Column("priority")]
        public int Priority { get; set; } = 1;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CreatedBy))]
        public User? CreatedByUser { get; set; }
    }

    [Table("backup_logs")]
    public class BackupLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Column("backup_id")]
        public long? BackupId { get; set; }

        [Required]
        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Required]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("message")]
        public string? Message { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(BackupId))]
        public Backup? Backup { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public User? CreatedByUser { get; set; }
    }
}