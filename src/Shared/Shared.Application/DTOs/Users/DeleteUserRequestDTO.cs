using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class DeleteUserRequest
    {
        [Required]
        public Guid Id { get; set; }
        
        public bool Permanent { get; set; } = false; // false = soft delete, true = permanent delete
    }
}