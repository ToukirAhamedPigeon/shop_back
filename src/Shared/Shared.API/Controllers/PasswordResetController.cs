using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;

[ApiController]
[Route("api/[controller]")]
public class PasswordResetController : ControllerBase
{
    private readonly IPasswordResetService _resetService;

    public PasswordResetController(IPasswordResetService resetService)
    {
        _resetService = resetService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> Request([FromBody] string email)
    {
        try
        {
            await _resetService.RequestPasswordResetAsync(email);
            return Ok(new { message = "Password reset email sent." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("validate/{token}")]
    public async Task<IActionResult> ValidateToken(string token)
    {
        var valid = await _resetService.ValidateTokenAsync(token);
        if (!valid) return BadRequest(new { message = "Invalid or expired link." });
        return Ok(new { valid = true });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromBody] ResetPasswordRequestDto request)
    {
        try
        {
            await _resetService.ResetPasswordAsync(request);
            return Ok(new { message = "Password successfully reset." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
