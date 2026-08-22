// src/Shared/Shared.Application/DTOs/Documentation/PagedChangelogDto.cs
namespace shop_back.src.Shared.Application.DTOs.Documentation
{
    public class PagedChangelogDto
    {
        public List<ChangelogEntryDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
