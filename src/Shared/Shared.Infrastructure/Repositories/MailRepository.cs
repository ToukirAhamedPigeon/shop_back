// src/Shared/Infrastructure/Repositories/MailRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.DTOs.Mails;
using shop_back.src.Shared.Infrastructure.Data;
using System.Linq.Dynamic.Core;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class MailRepository : IMailRepository
    {
        private readonly AppDbContext _context;

        public MailRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Mail?> GetByIdAsync(long id)
        {
            return await _context.Mails
                .Include(m => m.CreatedByUser)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsTrash);
        }

        public async Task<Mail?> GetByIdWithRepliesAsync(long id)
        {
            return await _context.Mails
                .Include(m => m.CreatedByUser)
                .Include(m => m.Replies)
                .ThenInclude(r => r.CreatedByUser)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsTrash);
        }

        public async Task<(IEnumerable<Mail> Items, int TotalCount, int GrandTotalCount)> GetFilteredAsync(MailFilterRequest request)
        {
            IQueryable<Mail> query = _context.Mails
                .Include(m => m.CreatedByUser)
                .Where(m => !m.IsTrash);

            // Filter by mailbox
            switch (request.Mailbox?.ToLower())
            {
                case "inbox":
                    query = query.Where(m => m.IsReceived && !m.IsSent);
                    break;
                case "sent":
                    query = query.Where(m => m.IsSent);
                    break;
                case "starred":
                    query = query.Where(m => m.IsStarred);
                    break;
                case "trash":
                    query = _context.Mails.Where(m => m.IsTrash);
                    break;
            }

            // Search
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.ToLower();
                query = query.Where(m =>
                    m.Subject.ToLower().Contains(q) ||
                    m.Body.ToLower().Contains(q) ||
                    m.FromMail.ToLower().Contains(q) ||
                    m.ToMail.ToLower().Contains(q));
            }

            // Date range
            if (request.FromDate.HasValue)
                query = query.Where(m => m.CreatedAt >= request.FromDate.Value);
            if (request.ToDate.HasValue)
                query = query.Where(m => m.CreatedAt <= request.ToDate.Value);

            // Mail type
            if (!string.IsNullOrWhiteSpace(request.MailType))
                query = query.Where(m => m.MailType == request.MailType);

            // Purpose
            if (!string.IsNullOrWhiteSpace(request.Purpose))
                query = query.Where(m => m.Purpose == request.Purpose);

            // Read status
            if (request.IsRead.HasValue)
                query = query.Where(m => m.IsRead == request.IsRead.Value);

            // Starred status
            if (request.IsStarred.HasValue)
                query = query.Where(m => m.IsStarred == request.IsStarred.Value);

            // Get grand total
            int grandTotalCount = await _context.Mails.CountAsync();

            // Sorting
            var sortBy = request.SortBy?.ToLower() ?? "createdat";
            var sortOrder = request.SortOrder?.ToLower() == "desc" ? "descending" : "ascending";
            query = query.OrderBy($"{sortBy} {sortOrder}");

            // Get total count
            int totalCount = await query.CountAsync();

            // Pagination
            var items = await query
                .Skip((request.Page - 1) * request.Limit)
                .Take(request.Limit)
                .ToListAsync();

            return (items, totalCount, grandTotalCount);
        }

        public async Task AddAsync(Mail mail)
        {
            await _context.Mails.AddAsync(mail);
        }

        public Task UpdateAsync(Mail mail)
        {
            _context.Mails.Update(mail);
            return Task.CompletedTask;
        }

       public Task DeleteAsync(Mail mail)
       {
           mail.IsTrash = true;
           mail.UpdatedAt = DateTime.UtcNow;
           _context.Mails.Update(mail);
           return Task.CompletedTask;
       }

        public async Task DeletePermanentlyAsync(long id)
        {
            var mail = await _context.Mails.FindAsync(id);
            if (mail != null)
            {
                _context.Mails.Remove(mail);
            }
        }

        public async Task BulkUpdateAsync(List<long> ids, Action<Mail> updateAction)
        {
            var mails = await _context.Mails
                .Where(m => ids.Contains(m.Id))
                .ToListAsync();

            foreach (var mail in mails)
            {
                updateAction(mail);
                mail.UpdatedAt = DateTime.UtcNow;
            }

            _context.Mails.UpdateRange(mails);
        }

        public async Task<int> GetUnreadCountAsync()
        {
            return await _context.Mails
                .CountAsync(m => m.IsReceived && !m.IsRead && !m.IsTrash);
        }

        public async Task<MailStatisticsDto> GetStatisticsAsync()
        {
            return new MailStatisticsDto
            {
                TotalSent = await _context.Mails.CountAsync(m => m.IsSent && !m.IsTrash),
                TotalReceived = await _context.Mails.CountAsync(m => m.IsReceived && !m.IsTrash),
                UnreadCount = await _context.Mails.CountAsync(m => m.IsReceived && !m.IsRead && !m.IsTrash),
                StarredCount = await _context.Mails.CountAsync(m => m.IsStarred && !m.IsTrash),
                TrashCount = await _context.Mails.CountAsync(m => m.IsTrash)
            };
        }

        public async Task<bool> ExistsByMessageIdAsync(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return false;
            
            return await _context.Mails.AnyAsync(m => m.MessageId == messageId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}