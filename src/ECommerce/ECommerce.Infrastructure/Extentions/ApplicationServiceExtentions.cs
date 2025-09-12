using Microsoft.Extensions.DependencyInjection;

// ECommerce application layer
using shop_back.src.ECommerce.Application.Services;
using shop_back.src.ECommerce.Application.Interfaces;


namespace shop_back.src.ECommerce.Infrastructure.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IProductService, ProductService>();

            return services;
        }
    }
}
