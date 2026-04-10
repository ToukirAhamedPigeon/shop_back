using System;

namespace shop_back.src.Shared.Application.DTOs.Options
{
    public class OptionFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        
        public string? IsActiveStr { get; set; }
        public string? IsDeletedStr { get; set; }
        
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        
        public string? ParentId { get; set; }
        
        public bool? IsActive => ParseBool(IsActiveStr);
        public bool? IsDeleted => ParseBool(IsDeletedStr);
        
        // Helper method to get the actual parent filter value - Fixed null handling
        public Guid? GetParentIdFilter()
        {
            if (string.IsNullOrEmpty(ParentId) || ParentId == "all")
                return null;
            
            if (ParentId == "null")
                return null; // Return null, FilterByNullParent will handle this case
            
            if (Guid.TryParse(ParentId, out var guid))
                return guid;
            
            return null;
        }
        
        // Helper to check if we should filter by NULL parent
        public bool FilterByNullParent => ParentId == "null";
        
        private static bool? ParseBool(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            if (val.Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;
            if (bool.TryParse(val, out var b)) 
                return b;
            return null;
        }
    }
}