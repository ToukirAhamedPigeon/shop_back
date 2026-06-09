// src/Shared/Application/Repositories/IMailRepository.cs
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Mails;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IMailRepository
    {
        Task<Mail?> GetByIdAsync(long id);
        Task<Mail?> GetByIdWithRepliesAsync(long id);
        Task<(IEnumerable<Mail> Items, int TotalCount, int GrandTotalCount)> GetFilteredAsync(MailFilterRequest request);
        Task AddAsync(Mail mail);
        Task UpdateAsync(Mail mail);
        Task DeleteAsync(Mail mail);
        Task DeletePermanentlyAsync(long id);
        Task BulkUpdateAsync(List<long> ids, Action<Mail> updateAction);
        Task<int> GetUnreadCountAsync();
        Task<MailStatisticsDto> GetStatisticsAsync();
        Task SaveChangesAsync();
        Task<bool> ExistsByMessageIdAsync(string messageId);
    }

    public interface IMailTemplateRepository
    {
        Task<MailTemplate?> GetByIdAsync(long id);
        Task<(IEnumerable<MailTemplate> Items, int TotalCount)> GetFilteredAsync(string? q, int page, int limit, bool includeGlobal, Guid? userId);
        Task AddAsync(MailTemplate template);
        Task UpdateAsync(MailTemplate template);
        Task DeleteAsync(MailTemplate template);
        Task<bool> ExistsByNameAsync(string name, long? ignoreId = null);
        Task SaveChangesAsync();
    }

    public interface IMailAttachmentRepository
    {
        Task AddAsync(MailAttachment attachment);
        Task<List<MailAttachment>> GetByMailIdAsync(long mailId);
        Task DeleteByMailIdAsync(long mailId);

        Task SaveChangesAsync();
    }
}