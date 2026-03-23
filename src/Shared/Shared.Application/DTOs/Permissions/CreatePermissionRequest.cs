using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Permissions
{
    public class CreatePermissionRequest
    {
        [Required]
        public string Names { get; set; } = string.Empty; // Multiple names separated by "="
        
        [Required]
        public string GuardName { get; set; } = "admin";
        
        public List<string> Roles { get; set; } = new();
        
        public string? IsActive { get; set; } // "true"/"false"
    }
}