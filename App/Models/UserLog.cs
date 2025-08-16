using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
    [Table("user_logs")]
    public class UserLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("detail")]
        public string? Detail { get; set; }

        [Column("changes")]
        public string? Changes { get; set; } // JSONB → stored as string

        [Required]
        [Column("action_type")]
        public string ActionType { get; set; } = string.Empty; // "Create", "Update", "Delete"

        [Required]
        [Column("model_name")]
        public string ModelName { get; set; } = string.Empty;

        [Column("model_id")]
        public Guid? ModelId { get; set; }

        [Required]
        [Column("created_by")]
        public Guid CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_at_id")]
        public long CreatedAtId { get; set; }
    }
}
