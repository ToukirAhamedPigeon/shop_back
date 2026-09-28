using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.PermissionGroups
{
    public class PermissionGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string[] Permissions { get; set; } = Array.Empty<string>();
        /// <summary>Roles that have this group.</summary>
        public string[] Roles { get; set; } = Array.Empty<string>();
        /// <summary>Users given this group directly (not through a role).</summary>
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SavePermissionGroupRequest
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>"true" / "false", like the role and permission forms.</summary>
        public string? IsActive { get; set; }

        public List<string> Permissions { get; set; } = new();
    }
}
