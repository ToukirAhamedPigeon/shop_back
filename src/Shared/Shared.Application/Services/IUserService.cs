using shop_back.src.Shared.Application.DTOs.Users;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserService
    {
        Task<object> GetUsersAsync(UserFilterRequest request);
        Task<UserDto?> GetUserAsync(Guid id);
    }
}
