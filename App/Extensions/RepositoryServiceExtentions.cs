using Microsoft.Extensions.DependencyInjection;
using server.App.Repositories;
using server.App.Models;
using System;

namespace server.App.Extensions
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Register both the generic and specific repository interfaces
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IRepository<Product, Guid>, ProductRepository>();

            return services;
        }
    }
}
