using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface ITranslationRepository
    {
        Task<IEnumerable<TranslationValue>> GetByLangAsync(string lang, string? module = null, CancellationToken ct = default);
        Task<TranslationKey?> GetKeyAsync(string module, string key, CancellationToken ct = default);
        Task AddOrUpdateAsync(string module, string key, string lang, string value, CancellationToken ct = default);
    }
}
