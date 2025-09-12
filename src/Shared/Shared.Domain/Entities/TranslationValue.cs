using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
  [Table("translation_values", Schema = "public")]
    public class TranslationValue
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("key_id")]        // <--- map this
        public long KeyId { get; set; }

        [Required]
        [Column("lang")]
        public string Lang { get; set; } = null!;

        [Required]
        [Column("value")]
        public string Value { get; set; } = null!;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [ForeignKey(nameof(KeyId))]
        public TranslationKey Key { get; set; } = null!;
    }
}
