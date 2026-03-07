using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IPasswordResetRepository
    {
        Task<PasswordReset?> GetByTokenAsync(string token, string tokenType);
        Task<IEnumerable<PasswordReset>> GetAllByUserAsync(Guid userId, string tokenType);
        Task AddAsync(PasswordReset passwordReset);
        Task UpdateAsync(PasswordReset passwordReset);
        Task SaveChangesAsync();
        Task MarkExistingTokensAsUsedAsync(Guid userId, string tokenType);
    }
}
