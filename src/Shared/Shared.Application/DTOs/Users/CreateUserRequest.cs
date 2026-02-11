using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class CreateUserRequest
    {
        // Identity
        [Required] public string Name { get; set; } = string.Empty;
        [Required, MinLength(4)] public string Username { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{6,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character.")]
        public string Password { get; set; } = string.Empty;
        [Required, Compare("Password")] public string ConfirmedPassword { get; set; } = string.Empty;
        public string? MobileNo { get; set; }
        public string? NID { get; set; }

        // Profile
        public IFormFile? ProfileImage { get; set; }
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? IsActive { get; set; } // "true"/"false"

        // Roles & Permissions
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}
