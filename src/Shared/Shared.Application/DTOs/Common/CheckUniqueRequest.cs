namespace shop_back.src.Shared.Application.DTOs.Common
{
    public class CheckUniqueRequest
    {
        public string Model { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string FieldValue { get; set; } = string.Empty;

        public string? ExceptFieldName { get; set; }
        public string? ExceptFieldValue { get; set; }
    }
}
