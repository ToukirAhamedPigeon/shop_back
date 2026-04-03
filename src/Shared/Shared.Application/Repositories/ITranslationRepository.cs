using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Translations;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface ITranslationRepository
    {
        Task<IEnumerable<TranslationValue>> GetByLangAsync(string lang, string? module = null, CancellationToken ct = default);
        Task<TranslationKey?> GetKeyAsync(string module, string key, CancellationToken ct = default);
        Task AddOrUpdateAsync(string module, string key, string lang, string value, CancellationToken ct = default);
        Task<(IEnumerable<TranslationDto> Translations, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
        GetFilteredTranslationsAsync(TranslationFilterRequest request, CancellationToken ct = default);
        
        Task<TranslationDto?> GetTranslationByIdAsync(long id, CancellationToken ct = default);
        Task<TranslationKey?> GetTranslationKeyWithValuesAsync(long id, CancellationToken ct = default);
        Task<bool> TranslationKeyExistsAsync(string module, string key, long? ignoreId = null, CancellationToken ct = default);
        Task<TranslationKey> CreateTranslationAsync(CreateTranslationRequest request, CancellationToken ct = default);
        Task UpdateTranslationAsync(long id, UpdateTranslationRequest request, CancellationToken ct = default);
        Task DeleteTranslationAsync(long id, CancellationToken ct = default);
        Task<List<string>> GetDistinctModulesAsync(CancellationToken ct = default);
    }
}
