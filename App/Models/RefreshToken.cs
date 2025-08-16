using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.App.Models
{
    [Table("refresh_tokens")]
    public class RefreshToken
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Required]
        [Column("is_revoked")]
        public bool IsRevoked { get; set; } = false;

        // Relation to User
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        // Track who updated/revoked this token
        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }  // Nullable in case it hasn't been updated yet

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }  // Nullable for the same reason
    }
}
