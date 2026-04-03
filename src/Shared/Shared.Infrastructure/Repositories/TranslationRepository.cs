using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Translations;
using System.Text.Json;

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

        public async Task<(IEnumerable<TranslationDto> Translations, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
            GetFilteredTranslationsAsync(TranslationFilterRequest request, CancellationToken ct = default)
        {
            IQueryable<TranslationKey> baseQuery = _context.TranslationKeys
                .Include(k => k.Values)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.Trim();
                baseQuery = baseQuery.Where(k => 
                    k.Key.Contains(q) || 
                    k.Module.Contains(q) ||
                    k.Values.Any(v => v.Value.Contains(q)));
            }

            // Apply module filter
            if (request.Modules != null && request.Modules.Any())
            {
                baseQuery = baseQuery.Where(k => request.Modules.Contains(k.Module));
            }

            // Apply date range filter
            if (request.StartDate.HasValue)
            {
                baseQuery = baseQuery.Where(k => k.CreatedAt >= request.StartDate.Value);
            }
            if (request.EndDate.HasValue)
            {
                var endDate = request.EndDate.Value.Date.AddDays(1);
                baseQuery = baseQuery.Where(k => k.CreatedAt < endDate);
            }

            // Get total count
            int totalCount = await baseQuery.CountAsync(ct);
            int grandTotalCount = await _context.TranslationKeys.CountAsync(ct);

            // Apply sorting
            bool desc = request.SortOrder?.ToLower() == "desc";
            var sortBy = request.SortBy?.ToLower();

            IOrderedQueryable<TranslationKey> query;
            query = sortBy switch
            {
                "key" => desc ? baseQuery.OrderByDescending(x => x.Key) : baseQuery.OrderBy(x => x.Key),
                "module" => desc ? baseQuery.OrderByDescending(x => x.Module) : baseQuery.OrderBy(x => x.Module),
                "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
                _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
            };

            // Apply pagination
            var translations = await query
                .Skip((request.Page - 1) * request.Limit)
                .Take(request.Limit)
                .ToListAsync(ct);

            // Map to DTOs
            var result = translations.Select(k => new TranslationDto
            {
                Id = k.Id,
                Key = k.Key,
                Module = k.Module,
                EnglishValue = k.Values.FirstOrDefault(v => v.Lang == "en")?.Value ?? string.Empty,
                BanglaValue = k.Values.FirstOrDefault(v => v.Lang == "bn")?.Value ?? string.Empty,
                CreatedAt = k.CreatedAt,
                UpdatedAt = null // You can add UpdatedAt to TranslationKey entity if needed
            }).ToList();

            return (result, totalCount, grandTotalCount, request.Page - 1, request.Limit);
        }

        public async Task<TranslationDto?> GetTranslationByIdAsync(long id, CancellationToken ct = default)
        {
            var translation = await _context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (translation == null)
                return null;

            return new TranslationDto
            {
                Id = translation.Id,
                Key = translation.Key,
                Module = translation.Module,
                EnglishValue = translation.Values.FirstOrDefault(v => v.Lang == "en")?.Value ?? string.Empty,
                BanglaValue = translation.Values.FirstOrDefault(v => v.Lang == "bn")?.Value ?? string.Empty,
                CreatedAt = translation.CreatedAt,
                UpdatedAt = null
            };
        }

        public async Task<TranslationKey?> GetTranslationKeyWithValuesAsync(long id, CancellationToken ct = default)
        {
            return await _context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Id == id, ct);
        }

        public async Task<bool> TranslationKeyExistsAsync(string module, string key, long? ignoreId = null, CancellationToken ct = default)
        {
            var query = _context.TranslationKeys
                .Where(k => k.Module == module && k.Key == key);

            if (ignoreId.HasValue)
                query = query.Where(k => k.Id != ignoreId.Value);

            return await query.AnyAsync(ct);
        }

        public async Task<TranslationKey> CreateTranslationAsync(CreateTranslationRequest request, CancellationToken ct = default)
        {
            // Create translation key
            var translationKey = new TranslationKey
            {
                Key = request.Key,
                Module = request.Module,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.TranslationKeys.Add(translationKey);
            await _context.SaveChangesAsync(ct);

            // Create translation values
            var englishValue = new TranslationValue
            {
                KeyId = translationKey.Id,
                Lang = "en",
                Value = request.EnglishValue,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var banglaValue = new TranslationValue
            {
                KeyId = translationKey.Id,
                Lang = "bn",
                Value = request.BanglaValue,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.TranslationValues.AddRange(englishValue, banglaValue);
            await _context.SaveChangesAsync(ct);

            return translationKey;
        }

        public async Task UpdateTranslationAsync(long id, UpdateTranslationRequest request, CancellationToken ct = default)
        {
            var translationKey = await _context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (translationKey == null)
                throw new KeyNotFoundException($"Translation with id {id} not found");

            // Update key and module
            translationKey.Key = request.Key;
            translationKey.Module = request.Module;

            // Update or create English value
            var englishValue = translationKey.Values.FirstOrDefault(v => v.Lang == "en");
            if (englishValue != null)
            {
                englishValue.Value = request.EnglishValue;
            }
            else
            {
                englishValue = new TranslationValue
                {
                    KeyId = translationKey.Id,
                    Lang = "en",
                    Value = request.EnglishValue,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _context.TranslationValues.Add(englishValue);
            }

            // Update or create Bangla value
            var banglaValue = translationKey.Values.FirstOrDefault(v => v.Lang == "bn");
            if (banglaValue != null)
            {
                banglaValue.Value = request.BanglaValue;
            }
            else
            {
                banglaValue = new TranslationValue
                {
                    KeyId = translationKey.Id,
                    Lang = "bn",
                    Value = request.BanglaValue,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _context.TranslationValues.Add(banglaValue);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteTranslationAsync(long id, CancellationToken ct = default)
        {
            var translationKey = await _context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (translationKey == null)
                throw new KeyNotFoundException($"Translation with id {id} not found");

            // Remove translation values
            _context.TranslationValues.RemoveRange(translationKey.Values);
            
            // Remove translation key
            _context.TranslationKeys.Remove(translationKey);
            
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<string>> GetDistinctModulesAsync(CancellationToken ct = default)
        {
            return await _context.TranslationKeys
                .Select(k => k.Module)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync(ct);
        }
    }
}