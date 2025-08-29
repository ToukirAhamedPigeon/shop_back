using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
     [Table("translation_keys", Schema = "public")]
    public class TranslationKey
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("key")]
        public string Key { get; set; } = null!;

        [Required]
        [Column("module")]
        public string Module { get; set; } = "common";

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<TranslationValue> Values { get; set; } = new List<TranslationValue>();
    }
}