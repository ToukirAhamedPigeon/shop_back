using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.Translations;

namespace shop_back.src.Shared.Application.Services
{
    public interface ITranslationService
    {
        /// <summary>Get flattened map of key -> value for requested lang & optional module.</summary>
        Task<IDictionary<string,string>> GetTranslationsAsync(string lang, string? module = null, bool forceDbFetch = false, CancellationToken ct = default);
        
        // New CRUD methods
        Task<object> GetTranslationsAsync(TranslationFilterRequest request, CancellationToken ct = default);
        Task<TranslationDto?> GetTranslationByIdAsync(long id, CancellationToken ct = default);
        Task<TranslationDto?> GetTranslationForEditAsync(long id, CancellationToken ct = default);
        Task<(bool Success, string Message)> CreateTranslationAsync(CreateTranslationRequest request, string? createdBy, CancellationToken ct = default);
        Task<(bool Success, string Message)> UpdateTranslationAsync(long id, UpdateTranslationRequest request, string? updatedBy, bool isDeveloper, CancellationToken ct = default);
        Task<(bool Success, string Message)> DeleteTranslationAsync(long id, string? deletedBy, CancellationToken ct = default);
        Task<List<SelectOptionDto>> GetModulesForOptionsAsync(CancellationToken ct = default);
    }
}