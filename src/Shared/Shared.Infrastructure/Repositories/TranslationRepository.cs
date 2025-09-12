using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class TranslationRepository : ITranslationRepository
    {
        private readonly AppDbContext _context;     

        public TranslationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TranslationValue>> GetByLangAsync(string lang, string? module = null, CancellationToken ct = default)
        {
            var query = _context.TranslationValues
                           .Include(v => v.Key)
                           .Where(v => v.Lang == lang);

            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(v => v.Key.Module == module);

            return await query.ToListAsync(ct);
        }

        public Task<TranslationKey?> GetKeyAsync(string module, string key, CancellationToken ct = default)
        {
            return _context.TranslationKeys
                      .Include(k => k.Values)
                      .FirstOrDefaultAsync(k => k.Module == module && k.Key == key, ct);
        }

        public async Task AddOrUpdateAsync(string module, string key, string lang, string value, CancellationToken ct = default)
        {
            var tkey = await _context.TranslationKeys.FirstOrDefaultAsync(k => k.Module == module && k.Key == key, ct);
            if (tkey == null)
            {
                tkey = new TranslationKey { Module = module, Key = key, CreatedAt = DateTimeOffset.UtcNow };
                _context.TranslationKeys.Add(tkey);
                await _context.SaveChangesAsync(ct); // ensure KeyId
            }

            var tval = await _context.TranslationValues.FirstOrDefaultAsync(v => v.KeyId == tkey.Id && v.Lang == lang, ct);
            if (tval == null)
            {
                tval = new TranslationValue { KeyId = tkey.Id, Lang = lang, Value = value, CreatedAt = DateTimeOffset.UtcNow };
                _context.TranslationValues.Add(tval);
            }
            else
            {
                tval.Value = value;
            }
            await _context.SaveChangesAsync(ct);
        }
    }
}
