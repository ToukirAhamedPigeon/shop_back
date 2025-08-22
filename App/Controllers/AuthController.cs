using Microsoft.AspNetCore.Mvc;
using shop_back.App.DTOs.Auth;
using shop_back.App.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace shop_back.App.Controllers
{
    /// <summary>
    /// Handles authentication-related operations:
    /// - Login (issue JWT + Refresh Token in HttpOnly cookie)
    /// - Refresh Token
    /// - Logout from one or all devices
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Authenticates a user with credentials and issues JWT + Refresh Token.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);
            if (result == null) return Unauthorized("Invalid credentials");

            // Store refresh token securely in HttpOnly cookie
            Response.Cookies.Append("refreshToken", result.RefreshToken!, new CookieOptions
            {
                HttpOnly = true, // not accessible by JS
                Secure = true,   // must be true in production (HTTPS)
                SameSite = SameSiteMode.Strict, // prevents CSRF in browsers
                Expires = DateTime.UtcNow.AddDays(7)
            });

            // Optional: prevent leaking refresh token in response body
            result.RefreshToken = string.Empty;

            return Ok(result);
        }

        /// <summary>
        /// Issues a new access token using the refresh token stored in cookie.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized("Missing refresh token");

            var result = await _authService.RefreshTokenAsync(refreshToken);
            if (result == null)
                return Unauthorized("Invalid refresh token");

            return Ok(result);
        }

        /// <summary>
        /// Logs out the current device (revokes the refresh token in cookie).
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                await _authService.LogoutAsync(refreshToken); // revoke this token
                Response.Cookies.Delete("refreshToken"); // delete from client
            }

            return Ok(new { message = "Logged out successfully" });
        }

        /// <summary>
        /// Logs out user from all devices (revokes all refresh tokens).
        /// </summary>
        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _authService.LogoutAllDevicesAsync(userId);

            return Ok(new { message = "Logged out from all devices" });
        }

        /// <summary>
        /// Logs out from all other devices except the current one.
        /// </summary>
        [Authorize]
        [HttpPost("logout-others")]
        public async Task<IActionResult> LogoutOthers()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.LogoutOtherDevicesAsync(refreshToken, userId);
            }

            return Ok(new { message = "Logged out from other devices" });
        }
    }
}
