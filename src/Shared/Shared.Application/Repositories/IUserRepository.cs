using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdentifierAsync(string identifier); // renamed for clarity
    }
}
