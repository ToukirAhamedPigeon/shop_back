using System.Text.Json.Nodes;
namespace shop_back.src.Shared.Application.DTOs.Common
{
    public class SelectRequestDto
    {
        /// <summary>Pagination limit. Defaults to 250 if not provided.</summary>
        public int Limit { get; set; } = 250;
    public int Skip { get; set; } = 0;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; } = "asc";

    // Accept any JSON value
    public Dictionary<string, JsonNode?>? Where { get; set; } = new();
    public string? Search { get; set; }
    }
}