using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("model_permissions")]
    public class ModelPermission
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("model_id")]
        public Guid ModelId { get; set; }

        [Column("permission_id")]
        public Guid? PermissionId { get; set; }

        [Column("model_name")]
        public string ModelName { get; set; } = "User";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Navigation
        [ForeignKey("PermissionId")]
        public virtual Permission? Permission { get; set; }
    }
}
