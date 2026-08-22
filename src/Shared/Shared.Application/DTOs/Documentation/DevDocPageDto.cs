// src/Shared/Shared.Application/DTOs/Documentation/DevDocPageDto.cs
namespace shop_back.src.Shared.Application.DTOs.Documentation
{
    public class DevDocPageDto
    {
        public string Title { get; set; } = string.Empty;
        public string Markdown { get; set; } = string.Empty;
        public List<string> SourcePaths { get; set; } = new();
        public DateTime? UpdatedAt { get; set; }
    }
}
