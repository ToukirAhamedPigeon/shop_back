namespace shop_back.App.DTOs
{
    using System.Text.Json.Serialization;
    public class ProductFilterDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; } // partial match
        [JsonPropertyName("minPrice")]
        public decimal? MinPrice { get; set; }
        [JsonPropertyName("maxPrice")]
        public decimal? MaxPrice { get; set; }
    }
}
