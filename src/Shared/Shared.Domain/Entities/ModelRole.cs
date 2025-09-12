using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("model_roles")]
    public class ModelRole
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("model_id")]
        public Guid ModelId { get; set; }

        [Required]
        [Column("role_id")]
        public Guid RoleId { get; set; }

        [Required]
        [Column("model_name")]
        public string ModelName { get; set; } = "User";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Navigation
        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }
    }
}
