using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
    [Table("roles")]
    public class Role
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("guard_name")]
        public string GuardName { get; set; } = "admin";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Navigation: Role can have many RolePermissions and many ModelRoles
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public virtual ICollection<ModelRole> ModelRoles { get; set; } = new List<ModelRole>();
    }
}
