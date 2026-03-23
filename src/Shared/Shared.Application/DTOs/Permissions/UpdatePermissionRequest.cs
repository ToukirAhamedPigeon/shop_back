using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Permissions
{
    public class UpdatePermissionRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string GuardName { get; set; } = "admin";
        
        public List<string> Roles { get; set; } = new();
        
        public string? IsActive { get; set; } // "true"/"false"
    }
}