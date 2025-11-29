using System;

namespace shop_back.src.Shared.Application.DTOs.UserLogs
{
    public class UserLogDto
    {
        public Guid Id { get; set; }
        public string? Detail { get; set; }
        public string? Changes { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public Guid? ModelId { get; set; }
        public string? CreatedByName { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public long CreatedAtId { get; set; }
        public string? IpAddress { get; set; }
        public string? Browser { get; set; }
        public string? Device { get; set; }
        public string? OperatingSystem { get; set; }
        public string? UserAgent { get; set; }
    }
}
