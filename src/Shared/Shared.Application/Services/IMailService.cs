// src/Shared/Application/Services/IMailService.cs
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Mails;

namespace shop_back.src.Shared.Application.Services
{
    public interface IMailService
    {
        // Send mail
        Task<Mail> SendEmailAsync(SendMailRequest request, Guid? userId = null);
        
        // Get mails
        Task<MailDetailDto?> GetMailByIdAsync(long id);
        Task<(IEnumerable<MailDto> Items, int TotalCount, int GrandTotalCount)> GetMailsAsync(MailFilterRequest request);
        
        // Mail actions
        Task MarkAsReadAsync(long id);
        Task MarkAsUnreadAsync(long id);
        Task ToggleStarAsync(long id);
        Task MoveToTrashAsync(long id);
        Task RestoreFromTrashAsync(long id);
        Task DeletePermanentlyAsync(long id);
        
        // Bulk actions
        Task<BulkOperationResponse> BulkActionAsync(BulkMailActionRequest request);
        
        // Statistics
        Task<MailStatisticsDto> GetStatisticsAsync();
        
        // Template management
        Task<MailTemplateDto> CreateTemplateAsync(MailTemplateRequest request, Guid userId);
        Task<MailTemplateDto> UpdateTemplateAsync(long id, MailTemplateRequest request, Guid userId);
        Task DeleteTemplateAsync(long id, Guid userId);
        Task<MailTemplateDto?> GetTemplateAsync(long id);
        Task<(IEnumerable<MailTemplateDto> Items, int TotalCount)> GetTemplatesAsync(string? q, int page, int limit, bool includeGlobal, Guid? userId);

        string BuildEmailTemplate(string bodyContent, string subject = "Notification");
        
        // Email fetching (for receiving mails)
        Task FetchAndStoreEmailsAsync();
    }
}