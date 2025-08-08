using shop_back.App.Models;
using shop_back.App.DTOs;

namespace shop_back.App.Repositories
{
    public interface IProductRepository : IRepository<Product, Guid>
    {
        Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter);
    }
}
