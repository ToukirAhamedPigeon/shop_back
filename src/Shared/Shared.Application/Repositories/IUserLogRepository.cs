using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserLogRepository
    {
        Task<UserLog> CreateAsync(UserLog log);
        Task<UserLog?> GetByIdAsync(Guid id);
        Task<IEnumerable<UserLog>> GetByUserIdAsync(Guid userId);
        Task<IEnumerable<UserLog>> GetAllAsync();
        Task SaveChangesAsync();
    }
}
