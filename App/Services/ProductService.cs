using shop_back.App.Models;
using shop_back.App.DTOs;
using shop_back.App.Repositories;

namespace shop_back.App.Services
{
    public class ProductService(IProductRepository _repo) : IProductService
    {

        public async Task<IEnumerable<Product>> GetAllAsync() =>
            await _repo.GetAllAsync();

        public async Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter) =>
            await _repo.GetFilteredAsync(filter);


        public async Task<Product?> GetByIdAsync(Guid id) =>
            await _repo.GetByIdAsync(id);

        public async Task<Product> CreateAsync(Product product)
        {
            await _repo.AddAsync(product);
            await _repo.SaveAsync();
            return product;
        }

        public async Task<bool> UpdateAsync(Guid id, Product updated)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            existing.Name = updated.Name;
            existing.Price = updated.Price;
            // Optionally update other fields...

            _repo.Update(existing);
            await _repo.SaveAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            _repo.Delete(existing);
            await _repo.SaveAsync();
            return true;
        }
    }
}
