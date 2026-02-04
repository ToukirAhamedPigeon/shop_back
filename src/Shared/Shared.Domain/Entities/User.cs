using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        // --------------------
        // Basic Identity
        // --------------------

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        // --------------------
        // Profile (Nullable)
        // --------------------

        [Column("profile_image")]
        public string? ProfileImage { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("gender")]
        public string? Gender { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        // --------------------
        // Contact & Verification
        // --------------------

        [Column("mobile_no")]
        public string? MobileNo { get; set; }

        [Column("email_verified_at")]
        public DateTime? EmailVerifiedAt { get; set; }

        // --------------------
        // QR Info (Nullable)
        // --------------------

        [Column("qr_code")]
        public string? QRCode { get; set; }

        // --------------------
        // Auth & Security
        // --------------------

        [Column("remember_token")]
        public string? RememberToken { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [Column("last_login_ip")]
        public string? LastLoginIp { get; set; }

        // --------------------
        // Preferences (Nullable)
        // --------------------

        [Column("timezone")]
        public string? Timezone { get; set; }

        [Column("language")]
        public string? Language { get; set; }

        // --------------------
        // System Flags
        // --------------------

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        // --------------------
        // Audit
        // --------------------

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }

        // --------------------
        // Navigation
        // --------------------

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
