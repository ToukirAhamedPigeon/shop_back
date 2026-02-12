using shop_back.src.Shared.Application.DTOs.Users;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserService
    {
        Task<object> GetUsersAsync(UserFilterRequest request);
        Task<UserDto?> GetUserAsync(Guid id);
        Task<(bool Success, string Message)> CreateUserAsync(CreateUserRequest request, string? createdBy);
        Task<UserDto?> RegenerateQrAsync(Guid id, string? currentUserId);

    }
}
