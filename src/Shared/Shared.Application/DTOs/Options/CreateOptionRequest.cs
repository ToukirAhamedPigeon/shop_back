using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Options
{
    public class CreateOptionRequest
    {
        [Required]
        public string Names { get; set; } = string.Empty; // Multiple names separated by "="
        
        public Guid? ParentId { get; set; }
        
        [Required]
        public string HasChild { get; set; } = "false"; // "true"/"false"
        
        public string? IsActive { get; set; } // "true"/"false"
    }
}