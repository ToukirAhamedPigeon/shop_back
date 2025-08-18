using shop_back.App.DTOs;
using shop_back.App.DTOs.Auth;

namespace shop_back.App.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto); // fixed return type
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string token); // bool not needed

        Task LogoutAllDevicesAsync(Guid userId);
    }
}
