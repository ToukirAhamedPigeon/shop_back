using System;
using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Options
{
    public class UpdateOptionRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public Guid? ParentId { get; set; }
        
        [Required]
        public string HasChild { get; set; } = "false";
        
        public string? IsActive { get; set; }
    }
}