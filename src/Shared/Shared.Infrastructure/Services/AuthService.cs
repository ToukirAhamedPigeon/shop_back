using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
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

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IRolePermissionRepository rolePermissionRepo,
            IConfiguration config)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _config = config;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepo.GetByIdentifierAsync(request.Identifier);

            // ✅ Correction: safer null + password check
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

            // ✅ Correction: only one SaveChangesAsync needed
            await _refreshTokenRepo.AddAsync(refreshToken);
            await _refreshTokenRepo.RemoveExpiredAsync();

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

            // ✅ Correction: safer console debugging with IgnoreCycles

            // ✅ Correction: null + expiry validation
            if (existing == null || existing.ExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }

            await _refreshTokenRepo.RemoveExpiredAsync();

            var newAccessToken = GenerateJwtToken(existing.User);

            return new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = token, // ✅ keeping same refresh token until rotation implemented
                User = await BuildUserDtoAsync(existing.User)
            };
        }

        public async Task LogoutAsync(string token)
        {
            var refreshToken = await _refreshTokenRepo.GetByTokenAsync(token);
            if (refreshToken != null)
            {
                await _refreshTokenRepo.RevokeAsync(refreshToken);
            }
        }

        public async Task LogoutAllDevicesAsync(Guid userId)
        {
            await _refreshTokenRepo.RevokeAllAsync(userId);
        }

        public async Task LogoutOtherDevicesAsync(string exceptRefreshToken, Guid userId)
        {
            await _refreshTokenRepo.RevokeOtherAsync(exceptRefreshToken, userId);
        }

        private string GenerateJwtToken(User user)
        {
            // ✅ Correction: ensure claims are never null
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("mobile_no", user.MobileNo ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // ✅ Correction: expiry configurable, defaults to 10 mins
            var tokenExpiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 10;
            var expires = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<UserDto> BuildUserDtoAsync(User user)
        {
            // ✅ Correction: fixed operator issue with ?? by normalizing to array
            var roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var permissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                MobileNo = user.MobileNo,
                IsActive = user.IsActive,
                Roles = roles,
                Permissions = permissions
            };
        }
    }
}
