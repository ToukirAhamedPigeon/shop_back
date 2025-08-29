using Microsoft.Extensions.DependencyInjection;
using shop_back.App.Services;

namespace shop_back.App.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITranslationService, TranslationService>();
            return services;
        }
    }
}
