using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Roles
{
    public class UpdateRoleRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string GuardName { get; set; } = "admin";
        
        public List<string> Permissions { get; set; } = new();
        
        public string? IsActive { get; set; } // "true"/"false"
    }
}