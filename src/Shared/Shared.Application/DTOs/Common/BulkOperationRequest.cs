// src/Shared/Application/DTOs/Common/BulkOperationRequest.cs

using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Common  // Make sure this matches
{
    public class BulkOperationRequest
    {
        public List<string> Ids { get; set; } = new();
        public bool Permanent { get; set; } = false;
        
        public List<Guid> GetGuids()
        {
            var guids = new List<Guid>();
            foreach (var id in Ids)
            {
                if (Guid.TryParse(id, out var guid))
                {
                    guids.Add(guid);
                }
            }
            return guids;
        }
        
        public (bool IsValid, List<string> InvalidIds) ValidateIds()
        {
            var invalidIds = new List<string>();
            foreach (var id in Ids)
            {
                if (!Guid.TryParse(id, out _))
                {
                    invalidIds.Add(id);
                }
            }
            return (invalidIds.Count == 0, invalidIds);
        }
    }

    public class BulkOperationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<BulkOperationError> Errors { get; set; } = new();
    }

    public class BulkOperationError
    {
        public Guid Id { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}