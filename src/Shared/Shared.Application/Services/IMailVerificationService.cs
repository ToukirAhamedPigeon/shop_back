using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Services
{
    public interface IMailVerificationService
    {
        Task SendVerificationEmailAsync(User user);
        Task<(bool Success, string Message)> VerifyTokenAsync(string token);
    }
}