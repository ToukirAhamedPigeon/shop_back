namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class UserFilterRequest
    {
        // Search & pagination
        public string? Q { get; set; }

        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;

        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";

        // ---------- STRING based booleans (from frontend)
        public string? IsActiveStr { get; set; }    // "true" | "false"
        public string? IsDeletedStr { get; set; }   // "true" | "false"

        // ---------- Multi-select filters
        public List<string>? Roles { get; set; }
        public List<string>? Permissions { get; set; }
        public List<string>? Gender { get; set; }

        public List<Guid>? CreatedBy { get; set; }
        public List<Guid>? UpdatedBy { get; set; }

        // ---------- Date filtering
        public List<string>? DateType { get; set; } // column names
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        // ---------- Parsed values (backend use only)
        public bool? IsActive => ParseBool(IsActiveStr);
        public bool? IsDeleted => ParseBool(IsDeletedStr);

        private static bool? ParseBool(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            if (bool.TryParse(val, out var b)) return b;
            return null;
        }
    }
}
