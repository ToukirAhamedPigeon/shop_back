using System;

namespace shop_back.src.Shared.Application.DTOs.Translations
{
    public class TranslationDto
    {
        public long Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string EnglishValue { get; set; } = string.Empty;
        public string BanglaValue { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }  // Changed from DateTimeOffset to DateTime
        public DateTime? UpdatedAt { get; set; }  // Changed from DateTimeOffset to DateTime
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? UpdatedByName { get; set; }
    }
}