using shop_back.App.Models;

namespace shop_back.App.Repositories
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task AddAsync(RefreshToken refreshToken);
        Task RevokeAsync(RefreshToken refreshToken);
        Task RevokeAllAsync(Guid userId);
        Task RevokeOtherAsync(string exceptRefreshToken, Guid userId);
        Task SaveChangesAsync();
        Task RemoveExpiredAsync();
    }
}
