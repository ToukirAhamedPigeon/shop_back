using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Roles
{
    public class CreateRoleRequest
    {
        [Required]
        public string Names { get; set; } = string.Empty; // Multiple names separated by "="
        
        [Required]
        public string GuardName { get; set; } = "admin";
        
        public List<string> Permissions { get; set; } = new();
        
        public string? IsActive { get; set; } // "true"/"false"
    }
}