using Microsoft.Extensions.DependencyInjection;

using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.Interfaces;

namespace shop_back.src.Shared.Infrastructure.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITranslationService, TranslationService>();

            return services;
        }
    }
}
