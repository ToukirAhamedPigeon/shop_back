namespace shop_back.src.Shared.Application.DTOs.Common
{
    public class RolePermissionFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        
        // Can be "true", "false", or "all"
        public string? IsActiveStr { get; set; }
        public string? IsDeletedStr { get; set; }
        
        public bool? IsActive => ParseBool(IsActiveStr);
        public bool? IsDeleted => ParseBool(IsDeletedStr);
        
        private static bool? ParseBool(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            
            // Handle "all" as null (no filter)
            if (val.Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;
                
            if (bool.TryParse(val, out var b)) 
                return b;
                
            return null;
        }
    }
}