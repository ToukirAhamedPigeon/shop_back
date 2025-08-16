using Microsoft.Extensions.DependencyInjection;
using shop_back.App.Repositories;
using shop_back.App.Models;
using System;

namespace shop_back.App.Extensions
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Register both the generic and specific repository interfaces
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IRepository<Product, Guid>, ProductRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            return services;
        }
    }
}
