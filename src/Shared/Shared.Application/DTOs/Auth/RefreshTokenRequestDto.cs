using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Auth
{
    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
