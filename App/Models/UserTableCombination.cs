using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
    [Table("user_table_combinations")]
    public class UserTableCombination
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("table_id")]
        public Guid TableId { get; set; }

        [Column("show_column_combinations")]
        public string[] ShowColumnCombinations { get; set; } = Array.Empty<string>();

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
