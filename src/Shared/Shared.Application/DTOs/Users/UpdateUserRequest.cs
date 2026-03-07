using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class UpdateUserRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required, MinLength(4)]
        public string Username { get; set; } = string.Empty;
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? ConfirmedPassword { get; set; }

        [FromForm(Name = "mobile_no")]
        public string? MobileNo { get; set; }
        [FromForm(Name = "nid")]
        public string? NID { get; set; }
        [FromForm(Name = "profile_image")]
        public IFormFile? ProfileImage { get; set; }
        [FromForm(Name = "remove_profile_image")]
        public bool RemoveProfileImage { get; set; }
        [FromForm(Name = "address")]
        public string? Address { get; set; }
         [FromForm(Name = "is_active")]
        public string? IsActive { get; set; }

        public List<string> Roles { get; set; } = new();
        public List<string>? Permissions { get; set; }
    }
}
