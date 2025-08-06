using Microsoft.EntityFrameworkCore;
using server.App.DTOs;
using server.App.Models;
using server.App.Repositories;
using server.App.Data;

namespace server.App.Repositories
{
    public class ProductRepository(AppDbContext _context) : IProductRepository
    {
        public async Task<IEnumerable<Product>> GetAllAsync() =>
            await _context.Products.ToListAsync();

        public async Task<Product?> GetByIdAsync(Guid id) =>
            await _context.Products.FindAsync(id);

        public async Task AddAsync(Product entity) =>
            await _context.Products.AddAsync(entity);

        public void Update(Product entity) =>
            _context.Products.Update(entity);

        public void Delete(Product entity) =>
            _context.Products.Remove(entity);

        public async Task SaveAsync() =>
            await _context.SaveChangesAsync();

        // ✅ Implement GetFilteredAsync here
        public async Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                query = query.Where(p => p.Name.Contains(filter.Name));
            }

            if (filter.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);
            }

            return await query.ToListAsync();
        }
    }
}
