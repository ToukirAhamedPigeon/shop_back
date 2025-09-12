using Microsoft.Extensions.DependencyInjection;


// ECommerce repositories
using shop_back.src.ECommerce.Application.Repositories;
using shop_back.src.ECommerce.Infrastructure.Repositories;

namespace shop_back.src.ECommerce.Infrastructure.Extensions
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IProductRepository, ProductRepository>();

            return services;
        }
    }
}
