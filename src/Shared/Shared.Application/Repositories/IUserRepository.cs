using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdentifierAsync(string identifier); // renamed for clarity
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByMobileNoAsync(string mobileNo);
        Task<(IEnumerable<UserDto> Users, int TotalCount)> GetFilteredAsync(UserFilterRequest req);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SaveChangesAsync();
    }
}
