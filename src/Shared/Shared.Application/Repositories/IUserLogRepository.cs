using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.UserLogs;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserLogRepository
    {
        Task<UserLog> CreateAsync(UserLog log);
        Task<UserLog?> GetByIdAsync(Guid id);
        Task<IEnumerable<UserLog>> GetByUserIdAsync(Guid userId);
        Task<IEnumerable<UserLog>> GetAllAsync();
        Task SaveChangesAsync();
        Task<(IEnumerable<UserLogDto> Logs, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> GetFilteredAsync(UserLogFilterRequest req);
    }
}
