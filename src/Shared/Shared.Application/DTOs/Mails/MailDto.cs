// src/Shared/Application/DTOs/Mails/MailDto.cs
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace shop_back.src.Shared.Application.DTOs.Mails
{
    public class MailDto
    {
        public long Id { get; set; }
        public string FromMail { get; set; } = string.Empty;
        public string ToMail { get; set; } = string.Empty;
        public string? CcMail { get; set; }
        public string? BccMail { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ModuleName { get; set; }
        public string? Purpose { get; set; }
        public List<string> Attachments { get; set; } = new();
        public bool IsSent { get; set; }
        public bool IsReceived { get; set; }
        public bool IsRead { get; set; }
        public bool IsStarred { get; set; }
        public bool IsTrash { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? MailType { get; set; }
        public long? ParentMailId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MailDetailDto : MailDto
    {
        public List<MailDto> Replies { get; set; } = new();
        public string? InReplyTo { get; set; }
        public string? MessageId { get; set; }
    }

    public class SendMailRequest
    {
        public string ToMail { get; set; } = string.Empty;
        public string? CcMail { get; set; }
        public string? BccMail { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ModuleName { get; set; }
        public string? Purpose { get; set; }
        public List<IFormFile>? Attachments { get; set; } // This requires Microsoft.AspNetCore.Http
        public long? ParentMailId { get; set; }
        public string? MailType { get; set; }
    }

    public class MailFilterRequest
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        public string? Mailbox { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? MailType { get; set; }
        public string? Purpose { get; set; }
        public bool? IsRead { get; set; }
        public bool? IsStarred { get; set; }
    }

    public class BulkMailActionRequest
    {
        public List<long> Ids { get; set; } = new();
        public string Action { get; set; } = string.Empty;
    }

    public class MailTemplateDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsGlobal { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MailTemplateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsGlobal { get; set; }
    }

    public class MailStatisticsDto
    {
        public int TotalSent { get; set; }
        public int TotalReceived { get; set; }
        public int UnreadCount { get; set; }
        public int StarredCount { get; set; }
        public int TrashCount { get; set; }
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
        public long Id { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}