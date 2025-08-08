using Microsoft.EntityFrameworkCore;
using shop_back.App.Models;

namespace shop_back.App.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedNever(); // 👈 prevents EF from treating GUID as identity
            });

            // Seed initial data
           modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = Guid.Parse("b67327a5-4b27-4ac2-8c94-4d6c1a0e10ab"),
                    Name = "Sample Product",
                    Price = 19.99M,
                    CreatedAt = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime()
                },
                new Product
                {
                    Id = Guid.Parse("61d4c29a-c5d4-4f8d-b4cb-d9ad3c2fc8fb"),
                    Name = "MacBook Pro",
                    Price = 1999.99M,
                    CreatedAt = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime()
                },
                new Product
                {
                    Id = Guid.Parse("9a7b1a43-efc7-46ad-9271-8a11a5f65c99"),
                    Name = "iPhone 15",
                    Price = 1299.00M,
                    CreatedAt = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime()
                },
                new Product
                {
                    Id = Guid.Parse("294ac8c0-889f-499e-a04f-cd8789143b9f"),
                    Name = "AirPods",
                    Price = 249.99M,
                    CreatedAt = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime()
                }
            );
        }
    }
}
