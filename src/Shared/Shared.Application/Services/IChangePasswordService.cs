using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Auth;

namespace shop_back.src.Shared.Application.Services
{
    public interface IChangePasswordService
    {
        Task<ChangePasswordResponseDto> RequestChangePasswordAsync(
            Guid userId, 
            ChangePasswordRequestDto request);
            
        Task<bool> ValidateChangeTokenAsync(string token);
        
        Task CompleteChangePasswordAsync(VerifyPasswordChangeDto request);
    }
}