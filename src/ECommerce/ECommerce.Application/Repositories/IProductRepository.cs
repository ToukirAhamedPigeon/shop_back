using System;
using shop_back.src.ECommerce.Domain.Entities;
using shop_back.src.ECommerce.Application.DTOs;
using shop_back.src.Shared.Application.Repositories;

namespace shop_back.src.ECommerce.Application.Repositories
{
    public interface IProductRepository : IRepository<Product, Guid>
    {
        Task<IEnumerable<Product>> GetFilteredAsync(ProductFilterDto filter);
    }
}
