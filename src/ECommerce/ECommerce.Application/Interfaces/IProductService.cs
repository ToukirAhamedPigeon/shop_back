using shop_back.src.ECommerce.Domain.Entities;
using shop_back.src.ECommerce.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace shop_back.src.ECommerce.Application.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync();

        Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter);

        Task<Product?> GetByIdAsync(Guid id);
        Task<Product> CreateAsync(Product product);
        Task<bool> UpdateAsync(Guid id, Product updated);
        Task<bool> DeleteAsync(Guid id);
    }
}
