using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Services
{
    public interface IOptionsService
    {
        /// <summary>
        /// Get select options for a given type (like collections, actionTypes, creators)
        /// </summary>
        /// <param name="type">The option type</param>
        /// <param name="req">Filter/pagination/search</param>
        Task<IEnumerable<SelectOptionDto>> GetOptionsAsync(string type, SelectRequestDto req);
    }
}
