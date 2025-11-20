using Microsoft.Extensions.DependencyInjection;

// Shared repositories
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Repositories;


namespace shop_back.src.Shared.Infrastructure.Extensions
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Shared repos
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
            services.AddScoped<ITranslationRepository, TranslationRepository>();
            services.AddScoped<IMailRepository, MailRepository>();
            services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
            services.AddScoped<IOtpRepository, OtpRepository>();
            return services;
        }
    }
}
