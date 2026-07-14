// D:\shop\shop_back\src\Shared\Shared.Domain\Entities\AppSetting.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("app_settings")]
    public class AppSetting
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("category")]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Column("key")]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Column("value")]
        public string? Value { get; set; }

        [Column("data_type")]
        [MaxLength(50)]
        public string DataType { get; set; } = "text"; // text, boolean, color, image, select, textarea, number

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_encrypted")]
        public bool IsEncrypted { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("CreatedBy")]
        public virtual User? CreatedByUser { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual User? UpdatedByUser { get; set; }
    }
}