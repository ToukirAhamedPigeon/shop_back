using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IPasswordResetRepository
    {
        Task<PasswordReset?> GetByTokenAsync(string token);
        Task<IEnumerable<PasswordReset>> GetAllByUserAsync(Guid userId);
        Task AddAsync(PasswordReset passwordReset);
        Task UpdateAsync(PasswordReset passwordReset);
        Task SaveChangesAsync();
    }
}
