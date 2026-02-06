namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }

        // Identity
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? MobileNo { get; set; }

        // Profile
        public string? ProfileImage { get; set; }
        public string? Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }

        // QR
        public string? QRCode { get; set; }

        // Preferences
        public string? Timezone { get; set; }
        public string? Language { get; set; }

        // Status
        public bool IsActive { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string[] Roles { get; set; } = Array.Empty<string>();
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }
}
