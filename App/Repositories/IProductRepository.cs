using server.App.Models;
using server.App.DTOs;

namespace server.App.Repositories
{
    public interface IProductRepository : IRepository<Product, Guid>
    {
        Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter);
    }
}
