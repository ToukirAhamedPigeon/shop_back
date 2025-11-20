using Microsoft.Extensions.DependencyInjection;

using shop_back.src.Shared.Infrastructure.Services;
using shop_back.src.Shared.Application.Services;

namespace shop_back.src.Shared.Infrastructure.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITranslationService, TranslationService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<IMailService, MailService>();

            return services;
        }
    }
}
