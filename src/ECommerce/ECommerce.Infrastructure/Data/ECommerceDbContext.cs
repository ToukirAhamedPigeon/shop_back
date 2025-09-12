using Microsoft.EntityFrameworkCore;
using shop_back.src.ECommerce.Domain.Entities;

namespace shop_back.src.ECommerce.Infrastructure.Data
{
    public class ECommerceDbContext : DbContext
    {
        public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
            : base(options) { }
// ECommerce entities
        public DbSet<Product> Products { get; set; } = null!;
    }
}