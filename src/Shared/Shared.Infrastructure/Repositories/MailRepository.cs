using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Application.Repositories;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class MailRepository : IMailRepository
    {
        private readonly AppDbContext _context;

        public MailRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Mail?> GetByIdAsync(int id)
        {
            return await _context.Mails.FindAsync(id);
        }

        public async Task<IEnumerable<Mail>> GetAllAsync()
        {
            return await _context.Mails
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
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

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
