using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IOtpRepository
    {
        Task<Otp?> GetByEmailAndPurposeAsync(string email, string purpose);
        Task<Otp?> GetByCodeHashAsync(string email, string codeHash);
        Task<IEnumerable<Otp>> GetAllByUserAsync(Guid userId);
        Task AddAsync(Otp otp);
        Task UpdateAsync(Otp otp);
        Task SaveChangesAsync();
    }
}
