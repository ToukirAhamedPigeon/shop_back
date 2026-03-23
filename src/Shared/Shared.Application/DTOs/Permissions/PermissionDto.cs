using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Permissions
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string GuardName { get; set; } = "admin";
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
    }
}