using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("user_table_combinations")]
    public class UserTableCombination
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("table_id")]
        public string TableId { get; set; } = string.Empty;

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
