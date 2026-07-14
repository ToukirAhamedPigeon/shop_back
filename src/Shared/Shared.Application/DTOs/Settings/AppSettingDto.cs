// D:\shop\shop_back\src\Shared\Shared.Application\DTOs\Settings\AppSettingDto.cs
using System;

namespace shop_back.src.Shared.Application.DTOs.Settings
{
    public class AppSettingDto
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string DataType { get; set; } = "text";
        public string? Description { get; set; }
        public bool IsEncrypted { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedByName { get; set; }
        public string? UpdatedByName { get; set; }
    }
}