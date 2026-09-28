using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    /// <summary>A permission group given directly to a model (a user), like ModelPermission.</summary>
    [Table("model_permission_groups")]
    public class ModelPermissionGroup
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("model_id")]
        public Guid ModelId { get; set; }

        [Column("model_name")]
        public string ModelName { get; set; } = "User";

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("GroupId")]
        public virtual PermissionGroup? Group { get; set; }
    }
}
