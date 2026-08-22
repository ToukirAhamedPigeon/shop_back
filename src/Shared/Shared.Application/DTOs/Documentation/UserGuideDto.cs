// src/Shared/Shared.Application/DTOs/Documentation/UserGuideDto.cs
namespace shop_back.src.Shared.Application.DTOs.Documentation
{
    public class UserGuideDto
    {
        public string Title { get; set; } = string.Empty;
        public string Markdown { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
    }
}
