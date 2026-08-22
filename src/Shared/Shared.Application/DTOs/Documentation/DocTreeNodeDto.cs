// src/Shared/Shared.Application/DTOs/Documentation/DocTreeNodeDto.cs
namespace shop_back.src.Shared.Application.DTOs.Documentation
{
    public class DocTreeNodeDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsFile { get; set; }
        public List<DocTreeNodeDto> Children { get; set; } = new();
    }
}
