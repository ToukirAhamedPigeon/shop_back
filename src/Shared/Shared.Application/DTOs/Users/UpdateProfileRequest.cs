using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class UpdateProfileRequest
    {
        [Required]
        [FromForm(Name = "name")]
        public string Name { get; set; } = string.Empty;

        [FromForm(Name = "email")]
        public string Email { get; set; } = string.Empty;

        [FromForm(Name = "mobile_no")]
        public string? MobileNo { get; set; }

        [FromForm(Name = "nid")]
        public string? NID { get; set; }

        [FromForm(Name = "address")]
        public string? Address { get; set; }

        [FromForm(Name = "bio")]
        public string? Bio { get; set; }

        [FromForm(Name = "gender")]
        public string? Gender { get; set; }

        [FromForm(Name = "date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [FromForm(Name = "profile_image")]
        public IFormFile? ProfileImage { get; set; }

        [FromForm(Name = "remove_profile_image")]
        public bool RemoveProfileImage { get; set; }
    }
}