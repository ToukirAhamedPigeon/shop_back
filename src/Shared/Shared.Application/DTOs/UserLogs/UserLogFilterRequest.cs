using System;

namespace shop_back.src.Shared.Application.DTOs.UserLogs
{
    public class UserLogFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";

        public DateTime? CreatedAtFrom { get; set; }
        public DateTime? CreatedAtTo { get; set; }

        public string[]? CollectionName { get; set; }
        public string[]? ActionType { get; set; }
        public string[]? CreatedBy { get; set; }
    }
}
