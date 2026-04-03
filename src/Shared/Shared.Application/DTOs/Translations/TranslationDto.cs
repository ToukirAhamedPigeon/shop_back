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
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}