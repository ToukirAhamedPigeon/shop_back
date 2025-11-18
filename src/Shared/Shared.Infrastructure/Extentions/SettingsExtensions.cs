using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using shop_back.src.Shared.Application.Settings;

namespace shop_back.src.Shared.Infrastructure.Extensions
{
    public static class SettingsExtensions
    {
        public static IServiceCollection AddSettings(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));
            // Add other app settings here if needed
            return services;
        }
    }
}
