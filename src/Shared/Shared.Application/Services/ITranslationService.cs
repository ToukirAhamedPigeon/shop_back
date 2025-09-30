using shop_back.src.Shared.Application.DTOs;

namespace shop_back.src.Shared.Application.Services
{
    public interface ITranslationService
    {
        /// <summary>Get flattened map of key -> value for requested lang & optional module.</summary>
        Task<IDictionary<string,string>> GetTranslationsAsync(string lang, string? module = null, bool forceDbFetch = false, CancellationToken ct = default);
    }
}