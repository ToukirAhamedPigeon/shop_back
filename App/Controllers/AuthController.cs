using Microsoft.AspNetCore.Mvc;
using shop_back.App.DTOs;
using shop_back.App.DTOs.Auth;
using shop_back.App.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace shop_back.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);
            if (result == null) return Unauthorized();
            // Set refresh token in HttpOnly cookie
            Response.Cookies.Append("refreshToken", result.RefreshToken!, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // true on production HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            // Remove refresh token from response body if you want
            result.RefreshToken = string.Empty;
            return Ok(result);
        }
        
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken)) return Unauthorized();
            var result = await _authService.RefreshTokenAsync(refreshToken);
            if (result == null) return Unauthorized();
            return Ok(result);
        }
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                await _authService.LogoutAsync(refreshToken); // revoke the token
                // Remove the cookie from the client
                Response.Cookies.Delete("refreshToken");
            }

            return Ok(new { message = "Logged out successfully" });
        }
        
        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _authService.LogoutAllDevicesAsync(userId);
            return Ok(new { message = "Logged out from all devices" });
        }
    }
}
