using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    /// <summary>A permission group given to a role.</summary>
    [Table("role_permission_groups")]
    public class RolePermissionGroup
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("role_id")]
        public Guid RoleId { get; set; }

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }

        [ForeignKey("GroupId")]
        public virtual PermissionGroup? Group { get; set; }
    }
}
