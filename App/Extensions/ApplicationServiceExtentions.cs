using Microsoft.Extensions.DependencyInjection;
using server.App.Services;

namespace server.App.Extensions
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
