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
        public bool HasVerifiedEmail { get; set; }
        public List<string> BlockingTables { get; set; } = new();
        public RelatedRecordsDetails RelatedRecordsDetails { get; set; } = new();
    }

    public class RelatedRecordsDetails
    {
        public bool HasUserLogs { get; set; }
        public bool HasRefreshTokens { get; set; }
        public bool HasPasswordResets { get; set; }
        public bool HasUserTableCombinations { get; set; }
        public bool HasMails { get; set; }
        public int UserLogsCount { get; set; }
        public int RefreshTokensCount { get; set; }
        public int PasswordResetsCount { get; set; }
        public int UserTableCombinationsCount { get; set; }
        public int MailsCount { get; set; }
    }
}