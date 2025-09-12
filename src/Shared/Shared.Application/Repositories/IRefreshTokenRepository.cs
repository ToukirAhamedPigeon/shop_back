using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
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
