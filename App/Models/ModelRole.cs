using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
    [Table("model_roles")]
    public class ModelRole
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("model_id")]
        public Guid ModelId { get; set; }

        [Column("role_id")]
        public Guid? RoleId { get; set; }

        [Column("model_name")]
        public string ModelName { get; set; } = "User";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
