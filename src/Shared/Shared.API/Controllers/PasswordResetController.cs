using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/auth/password-reset")]
    public class PasswordResetController : ControllerBase
    {
        private readonly IPasswordResetService _resetService;

        public PasswordResetController(IPasswordResetService resetService)
        {
            _resetService = resetService;
        }

        // 1️⃣ Request password reset email
        [HttpPost("request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset([FromBody] CreatePasswordResetRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _resetService.RequestPasswordResetAsync(request.Email);
                return Ok(new CreatePasswordResetResponseDto { Message = "Password reset email sent." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RequestPasswordResetAsync: {ex.Message}");
                return BadRequest(new CreatePasswordResetResponseDto { Message = ex.Message });
            }
        }

        // 2️⃣ Validate reset token
        [HttpGet("validate/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateToken(string token)
        {
            try
            {
                var valid = await _resetService.ValidateTokenAsync(token);
                if (!valid)
                    return BadRequest(new ValidateResetTokenResponseDto
                    {
                        IsValid = false,
                        Reason = "Invalid or expired token"
                    });

                return Ok(new ValidateResetTokenResponseDto
                {
                    IsValid = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ValidateTokenAsync: {ex.Message}");
                return BadRequest(new ValidateResetTokenResponseDto
                {
                    IsValid = false,
                    Reason = ex.Message
                });
            }
        }

        // 3️⃣ Reset password
        [HttpPost("reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _resetService.ResetPasswordAsync(request);
                return Ok(new CreatePasswordResetResponseDto { Message = "Password successfully reset." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ResetPasswordAsync: {ex.Message}");
                return BadRequest(new CreatePasswordResetResponseDto { Message = ex.Message });
            }
        }
    }
}
