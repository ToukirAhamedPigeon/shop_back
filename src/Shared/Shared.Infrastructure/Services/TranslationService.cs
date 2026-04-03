using StackExchange.Redis;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.Translations;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Helpers;

namespace shop_back.src.Shared.Application.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly ITranslationRepository _repo;
        private readonly IDatabase _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(60);
        private readonly UserLogHelper _userLogHelper;

        public TranslationService(ITranslationRepository repo, IConnectionMultiplexer redis, UserLogHelper userLogHelper)
        {
            _repo = repo;
            _cache = redis.GetDatabase();
            _userLogHelper = userLogHelper;
        }

        private static string CacheKey(string lang, string? module) =>
            $"translations:{lang}:{module ?? "common"}";

        /// <summary>
        /// Get translations for a given language and module.
        /// </summary>
        public async Task<IDictionary<string, string>> GetTranslationsAsync(
            string lang,
            string? module = null,
            bool forceDbFetch = false,
            CancellationToken ct = default)
        {
            var cacheKey = CacheKey(lang, module);

            // 1️⃣ Try cache only if not forcing DB fetch
            if (!forceDbFetch)
            {
                var cached = await _cache.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(cached!) 
                           ?? new Dictionary<string, string>();
                }
            }

            // 2️⃣ Read from DB
            var rows = await _repo.GetByLangAsync(lang, module, ct);
            var map = rows.ToDictionary(r => $"{r.Key.Module}.{r.Key.Key}", r => r.Value);

            // 3️⃣ Cache the result (update cache)
            var serialized = System.Text.Json.JsonSerializer.Serialize(map);
            await _cache.StringSetAsync(cacheKey, serialized, _cacheTtl);

            return map;
        }

        public async Task<object> GetTranslationsAsync(TranslationFilterRequest request, CancellationToken ct = default)
        {
            var (translations, totalCount, grandTotalCount, pageIndex, pageSize) = 
                await _repo.GetFilteredTranslationsAsync(request, ct);
            
            return new
            {
                translations,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public async Task<TranslationDto?> GetTranslationByIdAsync(long id, CancellationToken ct = default)
        {
            return await _repo.GetTranslationByIdAsync(id, ct);
        }

        public async Task<TranslationDto?> GetTranslationForEditAsync(long id, CancellationToken ct = default)
        {
            return await GetTranslationByIdAsync(id, ct);
        }

        public async Task<(bool Success, string Message)> CreateTranslationAsync(
            CreateTranslationRequest request, 
            string? createdBy, 
            CancellationToken ct = default)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(request.Key))
                return (false, "Key is required");

            if (string.IsNullOrWhiteSpace(request.Module))
                return (false, "Module is required");

            if (string.IsNullOrWhiteSpace(request.EnglishValue))
                return (false, "English value is required");

            if (string.IsNullOrWhiteSpace(request.BanglaValue))
                return (false, "Bangla value is required");

            // Check if translation key already exists
            var exists = await _repo.TranslationKeyExistsAsync(request.Module, request.Key, null, ct);
            if (exists)
                return (false, $"Translation key '{request.Key}' already exists in module '{request.Module}'");

            // Parse created by
            Guid? createdByGuid = null;
            if (!string.IsNullOrEmpty(createdBy) && Guid.TryParse(createdBy, out var parsed))
                createdByGuid = parsed;

            try
            {
                // Create translation
                var translationKey = await _repo.CreateTranslationAsync(request, ct);

                // Clear cache for affected languages and modules
                await ClearCacheForTranslationAsync(request.Module, ct);

                // Log the action - Use Newtonsoft.Json for logging
                var afterSnapshot = new
                {
                    Id = translationKey.Id,
                    Key = translationKey.Key,
                    Module = translationKey.Module,
                    EnglishValue = request.EnglishValue,
                    BanglaValue = request.BanglaValue
                };

                var changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { before = (object?)null, after = afterSnapshot });

                await _userLogHelper.LogAsync(
                    userId: createdByGuid ?? Guid.Empty,
                    actionType: "Create",
                    detail: $"Translation '{translationKey.Key}' created in module '{translationKey.Module}'",
                    changes: changesJson,
                    modelName: "Translation",
                    modelId: _userLogHelper.GetGuidFromLong(translationKey.Id) 
                );

                return (true, "Translation created successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error creating translation: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateTranslationAsync(
            long id, 
            UpdateTranslationRequest request, 
            string? updatedBy, 
            bool isDeveloper,
            CancellationToken ct = default)
        {
            // Get existing translation
            var existing = await _repo.GetTranslationByIdAsync(id, ct);
            if (existing == null)
                return (false, "Translation not found");

            // Check if key is being changed and if user is developer
            if ((existing.Key != request.Key || existing.Module != request.Module) && !isDeveloper)
                return (false, "Only Developer type users can edit translation keys");

            // Check uniqueness if key or module changed
            if (existing.Key != request.Key || existing.Module != request.Module)
            {
                var exists = await _repo.TranslationKeyExistsAsync(request.Module, request.Key, id, ct);
                if (exists)
                    return (false, $"Translation key '{request.Key}' already exists in module '{request.Module}'");
            }

            // Parse updated by
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsed))
                updatedByGuid = parsed;

            // Get before snapshot for logging - Use anonymous object
            var beforeSnapshot = new
            {
                existing.Id,
                existing.Key,
                existing.Module,
                existing.EnglishValue,
                existing.BanglaValue
            };

            try
            {
                // Update translation
                await _repo.UpdateTranslationAsync(id, request, ct);

                // Clear cache for old and new modules
                await ClearCacheForTranslationAsync(existing.Module, ct);
                if (existing.Module != request.Module)
                    await ClearCacheForTranslationAsync(request.Module, ct);

                // Log the action - Use Newtonsoft.Json for logging
                var afterSnapshot = new
                {
                    Id = id,
                    Key = request.Key,
                    Module = request.Module,
                    EnglishValue = request.EnglishValue,
                    BanglaValue = request.BanglaValue
                };

                var changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { before = beforeSnapshot, after = afterSnapshot });

                await _userLogHelper.LogAsync(
                    userId: updatedByGuid ?? Guid.Empty,
                    actionType: "Update",
                    detail: $"Translation '{request.Key}' updated in module '{request.Module}'",
                    changes: changesJson,
                    modelName: "Translation",
                    modelId: _userLogHelper.GetGuidFromLong(id)
                );

                return (true, "Translation updated successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating translation: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteTranslationAsync(
            long id, 
            string? deletedBy, 
            CancellationToken ct = default)
        {
            // Get existing translation
            var existing = await _repo.GetTranslationByIdAsync(id, ct);
            if (existing == null)
                return (false, "Translation not found");

            // Parse deleted by
            Guid? deletedByGuid = null;
            if (!string.IsNullOrEmpty(deletedBy) && Guid.TryParse(deletedBy, out var parsed))
                deletedByGuid = parsed;

            try
            {
                // Get before snapshot for logging
                var beforeSnapshot = new
                {
                    existing.Id,
                    existing.Key,
                    existing.Module,
                    existing.EnglishValue,
                    existing.BanglaValue
                };

                // Delete translation
                await _repo.DeleteTranslationAsync(id, ct);

                // Clear cache for affected module
                await ClearCacheForTranslationAsync(existing.Module, ct);

                // Log the action - Use Newtonsoft.Json for logging
                var changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { before = beforeSnapshot, after = (object?)null });

                await _userLogHelper.LogAsync(
                    userId: deletedByGuid ?? Guid.Empty,
                    actionType: "Delete",
                    detail: $"Translation '{existing.Key}' deleted from module '{existing.Module}'",
                    changes: changesJson,
                    modelName: "Translation",
                    modelId: _userLogHelper.GetGuidFromLong(id)
                );

                return (true, "Translation deleted successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting translation: {ex.Message}");
            }
        }

        public async Task<List<SelectOptionDto>> GetModulesForOptionsAsync(CancellationToken ct = default)
        {
            var modules = await _repo.GetDistinctModulesAsync(ct);
            return modules.Select(m => new SelectOptionDto
            {
                Value = m,
                Label = m
            }).ToList();
        }

        private async Task ClearCacheForTranslationAsync(string module, CancellationToken ct = default)
        {
            // Clear cache for both languages
            var enCacheKey = CacheKey("en", module);
            var bnCacheKey = CacheKey("bn", module);
            
            await _cache.KeyDeleteAsync(enCacheKey);
            await _cache.KeyDeleteAsync(bnCacheKey);
        }
    }
}