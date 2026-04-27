using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Translations;
using System.Text.Json;
using shop_back.src.Shared.Application.DTOs.Common;

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
                tkey = new TranslationKey 
                { 
                    Module = module, 
                    Key = key, 
                    CreatedAt = DateTime.UtcNow  // Convert to UTC
                };
                _context.TranslationKeys.Add(tkey);
                await _context.SaveChangesAsync(ct);
            }

            var tval = await _context.TranslationValues.FirstOrDefaultAsync(v => v.KeyId == tkey.Id && v.Lang == lang, ct);
            if (tval == null)
            {
                tval = new TranslationValue 
                { 
                    KeyId = tkey.Id, 
                    Lang = lang, 
                    Value = value, 
                    CreatedAt = DateTime.UtcNow  // Convert to UTC
                };
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

            // Apply date range filter - Convert to UTC
            if (request.StartDate.HasValue)
            {
                var startDateUtc = request.StartDate.Value.ToUniversalTime().Date;
                baseQuery = baseQuery.Where(k => k.CreatedAt >= startDateUtc);
            }

            if (request.EndDate.HasValue)
            {
                var endDateUtc = request.EndDate.Value.ToUniversalTime().Date.AddDays(1);
                baseQuery = baseQuery.Where(k => k.CreatedAt < endDateUtc);
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
                "updatedat" => desc ? baseQuery.OrderByDescending(x => x.UpdatedAt) : baseQuery.OrderBy(x => x.UpdatedAt),
                _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
            };

            // Apply pagination
            var translations = await query
                .Skip((request.Page - 1) * request.Limit)
                .Take(request.Limit)
                .ToListAsync(ct);

            // Get user names for created_by and updated_by
            var userIds = translations.SelectMany(t => new[] { t.CreatedBy, t.UpdatedBy })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var users = new Dictionary<Guid, string>();
            if (userIds.Any())
            {
                users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
            }

            // Map to DTOs
            var result = translations.Select(k => new TranslationDto
            {
                Id = k.Id,
                Key = k.Key,
                Module = k.Module,
                EnglishValue = k.Values.FirstOrDefault(v => v.Lang == "en")?.Value ?? string.Empty,
                BanglaValue = k.Values.FirstOrDefault(v => v.Lang == "bn")?.Value ?? string.Empty,
                CreatedAt = k.CreatedAt,  // Convert DateTime to DateTime
                UpdatedAt = k.UpdatedAt,  // Convert DateTime to DateTime
                CreatedBy = k.CreatedBy,
                UpdatedBy = k.UpdatedBy,
                CreatedByName = k.CreatedBy.HasValue && users.ContainsKey(k.CreatedBy.Value) ? users[k.CreatedBy.Value] : null,
                UpdatedByName = k.UpdatedBy.HasValue && users.ContainsKey(k.UpdatedBy.Value) ? users[k.UpdatedBy.Value] : null
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

            // Get user names
            string? createdByName = null;
            string? updatedByName = null;

            if (translation.CreatedBy.HasValue)
            {
                var creator = await _context.Users.FindAsync(new object[] { translation.CreatedBy.Value }, ct);
                createdByName = creator?.Name;
            }

            if (translation.UpdatedBy.HasValue)
            {
                var updater = await _context.Users.FindAsync(new object[] { translation.UpdatedBy.Value }, ct);
                updatedByName = updater?.Name;
            }

            return new TranslationDto
            {
                Id = translation.Id,
                Key = translation.Key,
                Module = translation.Module,
                EnglishValue = translation.Values.FirstOrDefault(v => v.Lang == "en")?.Value ?? string.Empty,
                BanglaValue = translation.Values.FirstOrDefault(v => v.Lang == "bn")?.Value ?? string.Empty,
                CreatedAt = translation.CreatedAt,  // Convert DateTime to DateTime
                UpdatedAt = translation.UpdatedAt,  // Convert DateTime to DateTime
                CreatedBy = translation.CreatedBy,
                UpdatedBy = translation.UpdatedBy,
                CreatedByName = createdByName,
                UpdatedByName = updatedByName
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

        public async Task<TranslationKey> CreateTranslationAsync(CreateTranslationRequest request, Guid? createdBy, CancellationToken ct = default)
        {
            // Create translation key - DO NOT set Id explicitly, let DB generate it
            var translationKey = new TranslationKey
            {
                Key = request.Key,
                Module = request.Module,
                CreatedAt = DateTime.UtcNow,  // Already UTC
                CreatedBy = createdBy
            };

            _context.TranslationKeys.Add(translationKey);
            await _context.SaveChangesAsync(ct);

            // Create translation values - Convert to UTC
            var englishValue = new TranslationValue
            {
                KeyId = translationKey.Id,
                Lang = "en",
                Value = request.EnglishValue,
                CreatedAt = DateTime.UtcNow  // Already UTC
            };

            var banglaValue = new TranslationValue
            {
                KeyId = translationKey.Id,
                Lang = "bn",
                Value = request.BanglaValue,
                CreatedAt = DateTime.UtcNow  // Already UTC
            };

            _context.TranslationValues.AddRange(englishValue, banglaValue);
            await _context.SaveChangesAsync(ct);

            return translationKey;
        }

       public async Task UpdateTranslationAsync(long id, UpdateTranslationRequest request, Guid? updatedBy, CancellationToken ct = default)
        {
            var translationKey = await _context.TranslationKeys
                .Include(k => k.Values)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (translationKey == null)
                throw new KeyNotFoundException($"Translation with id {id} not found");

            // Update key and module
            translationKey.Key = request.Key;
            translationKey.Module = request.Module;
            translationKey.UpdatedAt = DateTime.UtcNow;  // Convert to UTC
            translationKey.UpdatedBy = updatedBy;

            // Update or create English value
            var englishValue = translationKey.Values.FirstOrDefault(v => v.Lang == "en");
            if (englishValue != null)
            {
                englishValue.Value = request.EnglishValue;
                // Don't update CreatedAt for existing values
            }
            else
            {
                englishValue = new TranslationValue
                {
                    KeyId = translationKey.Id,
                    Lang = "en",
                    Value = request.EnglishValue,
                    CreatedAt = DateTime.UtcNow  // Convert to UTC
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
                    CreatedAt = DateTime.UtcNow  // Convert to UTC
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
        public async Task<BulkOperationResponse> BulkDeleteTranslationsAsync(List<long> ids, Guid? deletedBy, CancellationToken ct = default)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = ids.Count,
                SuccessCount = 0,
                FailedCount = 0,
                Success = true
            };

            using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var translationKey = await _context.TranslationKeys
                            .Include(k => k.Values)
                            .FirstOrDefaultAsync(k => k.Id == id, ct);

                        if (translationKey == null)
                        {
                            response.FailedCount++;
                            response.Errors.Add(new BulkOperationError
                            {
                                Id = Guid.NewGuid(), // Use new GUID since translation uses long
                                Error = $"Translation with id {id} not found"
                            });
                            response.Success = false;
                            continue;
                        }

                        // Remove translation values
                        _context.TranslationValues.RemoveRange(translationKey.Values);
                        
                        // Remove translation key
                        _context.TranslationKeys.Remove(translationKey);

                        response.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        response.FailedCount++;
                        response.Errors.Add(new BulkOperationError
                        {
                            Id = Guid.NewGuid(),
                            Error = ex.Message
                        });
                        response.Success = false;
                    }
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                response.Message = $"Processed {response.TotalCount} translations. Success: {response.SuccessCount}, Failed: {response.FailedCount}";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                response.Success = false;
                response.Message = $"Bulk operation failed: {ex.Message}";
            }

            return response;
        }
    }
}