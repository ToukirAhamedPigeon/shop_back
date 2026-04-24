// src/Shared/Application/DTOs/Common/DeleteEligibilityResponse.cs

using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Common
{
    public class DeleteEligibilityResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool CanBePermanent { get; set; }
        public bool HasRelatedRecords { get; set; }
        public List<string> BlockingTables { get; set; } = new();
    }
}