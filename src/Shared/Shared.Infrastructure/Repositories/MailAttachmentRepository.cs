// src/Shared/Infrastructure/Repositories/MailAttachmentRepository.cs
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class MailAttachmentRepository : IMailAttachmentRepository
    {
        private readonly AppDbContext _context;

        public MailAttachmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MailAttachment attachment)
        {
            await _context.Set<MailAttachment>().AddAsync(attachment);
        }

        public async Task AddRangeAsync(List<MailAttachment> attachments)
        {
            await _context.Set<MailAttachment>().AddRangeAsync(attachments);
        }

        public async Task<List<MailAttachment>> GetByMailIdAsync(long mailId)
        {
            return await _context.Set<MailAttachment>()
                .Where(a => a.MailId == mailId)
                .ToListAsync();
        }

        public async Task DeleteByMailIdAsync(long mailId)
        {
            var attachments = await _context.Set<MailAttachment>()
                .Where(a => a.MailId == mailId)
                .ToListAsync();
            
            _context.Set<MailAttachment>().RemoveRange(attachments);
        }

        public async Task<MailAttachment?> GetByHashAsync(string fileHash)
        {
            if (string.IsNullOrEmpty(fileHash))
                return null;
            
            return await _context.Set<MailAttachment>()
                .FirstOrDefaultAsync(a => a.FileHash == fileHash);
        }

        public async Task<List<MailAttachment>> GetByAttachmentPathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return new List<MailAttachment>();
            
            return await _context.Set<MailAttachment>()
                .Where(a => a.FilePath == filePath)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}