using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Helpers; // for UserLogHelper
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IRolePermissionRepository _rolePermissionRepo;
        private readonly IConfiguration _config;
        private readonly UserLogHelper _userLogHelper;

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IRolePermissionRepository rolePermissionRepo,
            IConfiguration config,
            UserLogHelper userLogHelper)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _config = config;
            _userLogHelper = userLogHelper;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepo.GetByIdentifierAsync(request.Identifier);

            if (user == null || string.IsNullOrWhiteSpace(user.Password) ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return null;
            }

            var accessToken = GenerateJwtToken(user);

            var refreshToken = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                UpdatedBy = user.Id,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await _refreshTokenRepo.AddAsync(refreshToken);
            await _refreshTokenRepo.RemoveExpiredAsync();

            // ✅ User log for login
            await _userLogHelper.LogAsync(
                userId: user.Id,
                actionType: "Login",
                detail: $"User '{user.Username}' logged in successfully.",
                modelName: "User",
                modelId: user.Id
            );

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                User = await BuildUserDtoAsync(user)
            };
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string token)
        {
            var existing = await _refreshTokenRepo.GetByTokenAsync(token);

            if (existing == null || existing.ExpiresAt <= DateTime.UtcNow)
                return null;

            await _refreshTokenRepo.RemoveExpiredAsync();

            var newAccessToken = GenerateJwtToken(existing.User);

            return new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = token,
                User = await BuildUserDtoAsync(existing.User)
            };
        }

        public async Task LogoutAsync(string token)
        {
            try
            {
                var refreshToken = await _refreshTokenRepo.GetByTokenAsync(token);
                if (refreshToken != null)
                {
                    await _refreshTokenRepo.RevokeAsync(refreshToken);

                    // ✅ User log for logout
                    await _userLogHelper.LogAsync(
                        userId: refreshToken.UserId,
                        actionType: "Logout",
                        detail: $"User logged out successfully.",
                        modelName: "User",
                        modelId: refreshToken.UserId
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LogoutAsync Error: " + ex.Message);
            }
        }

        public async Task LogoutAllDevicesAsync(Guid userId)
        {
            try
            {
                await _refreshTokenRepo.RevokeAllAsync(userId);

                // ✅ User log for logout all devices
                await _userLogHelper.LogAsync(
                    userId: userId,
                    actionType: "LogoutAllDevices",
                    detail: "User logged out from all devices.",
                    modelName: "User",
                    modelId: userId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("LogoutAllDevicesAsync Error: " + ex.Message);
            }
        }

        public async Task LogoutOtherDevicesAsync(string exceptRefreshToken, Guid userId)
        {
            try
            {
                await _refreshTokenRepo.RevokeOtherAsync(exceptRefreshToken, userId);

                // ✅ User log for logout other devices
                await _userLogHelper.LogAsync(
                    userId: userId,
                    actionType: "LogoutOtherDevices",
                    detail: "User logged out from other devices except current.",
                    modelName: "User",
                    modelId: userId
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("LogoutOtherDevicesAsync Error: " + ex.Message);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("mobile_no", user.MobileNo ?? string.Empty)
            };

            var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
            try { DotNetEnv.Env.Load(envPath); } catch { }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DotNetEnv.Env.GetString("JwtKey")!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenExpiryMinutes = int.TryParse(DotNetEnv.Env.GetString("JwtExpiryMinutes"), out var minutes) ? minutes : 10;
            var expires = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes);

            var token = new JwtSecurityToken(
                issuer: DotNetEnv.Env.GetString("JwtIssuer"),
                audience: DotNetEnv.Env.GetString("JwtAudience"),
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<UserDto> BuildUserDtoAsync(User user)
        {
            var roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var permissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                MobileNo = user.MobileNo ?? string.Empty,
                IsActive = user.IsActive,
                Roles = roles,
                Permissions = permissions
            };
        }
    }
}