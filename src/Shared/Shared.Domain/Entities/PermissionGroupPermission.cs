using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    /// <summary>A permission inside a permission group.</summary>
    [Table("permission_group_permissions")]
    public class PermissionGroupPermission
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("permission_id")]
        public Guid PermissionId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("GroupId")]
        public virtual PermissionGroup? Group { get; set; }

        [ForeignKey("PermissionId")]
        public virtual Permission? Permission { get; set; }
    }
}
