using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUniqueCheckService
    {
        Task<bool> ExistsAsync(CheckUniqueRequest request);
    }
}
