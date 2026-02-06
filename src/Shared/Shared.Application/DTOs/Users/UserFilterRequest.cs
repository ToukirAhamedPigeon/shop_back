namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class UserFilterRequest
    {
        public string? Q { get; set; }

        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;

        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";

        public bool? IsActive { get; set; }
    }
}
