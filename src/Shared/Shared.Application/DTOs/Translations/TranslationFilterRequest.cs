using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Translations
{
    public class TranslationFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public List<string>? Modules { get; set; }
    }
}