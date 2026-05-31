using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using shop_back.src.Shared.Infrastructure.Services;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;

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
            services.AddScoped<IMailVerificationService, MailVerificationService>();
            services.AddScoped<IUserLogService, UserLogService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IOptionsService, OptionsService>();
            services.AddScoped<IChangePasswordService, ChangePasswordService>();
            services.AddScoped<IUserTableCombinationService, UserTableCombinationService>();
            services.AddScoped<IUniqueCheckService, UniqueCheckService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
             // Register EmailFetchBackgroundService as Singleton
            services.AddSingleton<EmailFetchBackgroundService>();
            
            // Register as IEmailFetchService (same instance)
            services.AddSingleton<IEmailFetchService>(provider => 
                provider.GetRequiredService<EmailFetchBackgroundService>());
            
            // Register as IHostedService to start background execution
            services.AddSingleton<IHostedService>(provider => 
                provider.GetRequiredService<EmailFetchBackgroundService>());
            services.AddScoped<UserLogHelper>();
            services.AddHttpContextAccessor();

            return services;
        }
    }
}
