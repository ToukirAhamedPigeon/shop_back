using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IMailRepository
    {
        Task<Mail?> GetByIdAsync(int id);
        Task<IEnumerable<Mail>> GetAllAsync();
        Task AddAsync(Mail mail);
        Task UpdateAsync(Mail mail);
        Task SaveChangesAsync();
    }
}
