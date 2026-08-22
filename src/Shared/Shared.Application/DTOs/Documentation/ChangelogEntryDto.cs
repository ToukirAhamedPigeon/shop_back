// src/Shared/Shared.Application/DTOs/Documentation/ChangelogEntryDto.cs
namespace shop_back.src.Shared.Application.DTOs.Documentation
{
    public class ChangelogEntryDto
    {
        public string? Sha { get; set; }
        public string? Repo { get; set; }
        public DateTime? Date { get; set; }
        public string? Summary { get; set; }
        public string? Author { get; set; }
        public List<string>? Files { get; set; }
    }
}
