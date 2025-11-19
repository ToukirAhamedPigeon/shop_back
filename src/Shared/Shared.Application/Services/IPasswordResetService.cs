using shop_back.src.Shared.Application.DTOs.Auth;

public interface IPasswordResetService
{
    Task RequestPasswordResetAsync(string email);
    Task<bool> ValidateTokenAsync(string token);
    Task ResetPasswordAsync(ResetPasswordRequestDto request);
}
