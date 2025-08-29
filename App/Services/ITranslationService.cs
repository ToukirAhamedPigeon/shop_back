using shop_back.App.Models;
using shop_back.App.DTOs;

namespace shop_back.App.Services
{
    public interface ITranslationService
    {
        /// <summary>Get flattened map of key -> value for requested lang & optional module.</summary>
        Task<IDictionary<string,string>> GetTranslationsAsync(string lang, string? module = null, CancellationToken ct = default);
    }
}