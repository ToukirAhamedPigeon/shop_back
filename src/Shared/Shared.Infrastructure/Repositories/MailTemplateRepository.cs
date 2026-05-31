using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class MailTemplateRepository : IMailTemplateRepository
    {
        private readonly AppDbContext _context;

        public MailTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MailTemplate?> GetByIdAsync(long id)
        {
            return await _context.Set<MailTemplate>()
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<(IEnumerable<MailTemplate> Items, int TotalCount)> GetFilteredAsync(
            string? q, int page, int limit, bool includeGlobal, Guid? userId)
        {
            IQueryable<MailTemplate> query = _context.Set<MailTemplate>();

            if (includeGlobal)
            {
                query = query.Where(t => t.IsGlobal || (userId.HasValue && t.CreatedBy == userId));
            }
            else if (userId.HasValue)
            {
                query = query.Where(t => t.CreatedBy == userId);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(search) ||
                    t.Subject.ToLower().Contains(search) ||
                    (t.Description != null && t.Description.ToLower().Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task AddAsync(MailTemplate template)
        {
            await _context.Set<MailTemplate>().AddAsync(template);
        }

        // Remove async keyword since no await is used
        public Task UpdateAsync(MailTemplate template)
        {
            _context.Set<MailTemplate>().Update(template);
            return Task.CompletedTask;
        }

        // Remove async keyword since no await is used
        public Task DeleteAsync(MailTemplate template)
        {
            _context.Set<MailTemplate>().Remove(template);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsByNameAsync(string name, long? ignoreId = null)
        {
            var query = _context.Set<MailTemplate>().Where(t => t.Name == name);
            if (ignoreId.HasValue)
            {
                query = query.Where(t => t.Id != ignoreId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}