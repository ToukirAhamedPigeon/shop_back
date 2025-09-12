using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("role_permissions")]
    public class RolePermission
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("permission_id")]
        public Guid? PermissionId { get; set; }

        [Column("role_id")]
        public Guid? RoleId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Navigation
        [ForeignKey("PermissionId")]
        public virtual Permission? Permission { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }
    }
}
