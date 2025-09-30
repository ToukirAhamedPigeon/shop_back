using shop_back.src.Shared.Application.DTOs;
using shop_back.src.Shared.Application.DTOs.Auth;

namespace shop_back.src.Shared.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto); // fixed return type
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string token); // bool not needed

        Task LogoutAllDevicesAsync(Guid userId);
        Task LogoutOtherDevicesAsync(string exceptRefreshToken, Guid userId);
    }
}
