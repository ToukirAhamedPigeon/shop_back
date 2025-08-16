using shop_back.App.Models;

namespace shop_back.App.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdentifierAsync(string identifier); // renamed for clarity
    }
}
