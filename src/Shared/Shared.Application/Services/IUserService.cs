using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Application.DTOs.Users;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserService
    {
        Task<object> GetUsersAsync(UserFilterRequest request);
        Task<UserDto?> GetUserAsync(Guid id);
        Task<UserDto?> GetUserForEditAsync(Guid id);
        Task<(bool Success, string Message)> CreateUserAsync(CreateUserRequest request, string? createdBy);
        Task<UserDto?> RegenerateQrAsync(Guid id, string? currentUserId);
        Task<(bool Success, string Message)> UpdateUserAsync(Guid id, UpdateUserRequest request, string? currentUserId);
        Task<UserDto?> GetProfileAsync(Guid userId);
        Task<(bool Success, string Message)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<(bool Success, string Message)> RequestPasswordChangeAsync(
        Guid userId, 
        ChangePasswordRequestDto request);
        
        Task<(bool Success, string Message)> VerifyPasswordChangeAsync(string token);
        Task<(bool Success, string Message, string DeleteType)> DeleteUserAsync(
            Guid id, 
            bool permanent, 
            string? currentUserId);
            
        Task<(bool Success, string Message)> RestoreUserAsync(
            Guid id, 
            string? currentUserId);
            
        Task<(bool Success, string Message, bool CanBePermanent)> CheckDeleteEligibilityAsync(Guid id);
    }
}
