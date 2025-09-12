using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Supabase;
using shop_back.src.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace shop_back.src.Shared.Infrastructure.Extensions
{
    public static class SharedServiceExtensions
    {
        public static void AddSharedServices(this IServiceCollection services, IConfiguration configuration)
        {
            // -------------------------
            // 1. Database
            // -------------------------
            var connStr = configuration.GetConnectionString("DefaultConnection")
                          ?? throw new InvalidOperationException("DefaultConnection not set");
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));

            // -------------------------
            // 2. Redis
            // -------------------------
            var redisConn = configuration["Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConn))
            {
                var options = ConfigurationOptions.Parse(redisConn);
                options.Password = configuration["Redis:Password"];
                options.DefaultDatabase = int.Parse(configuration["Redis:Database"] ?? "0");
                options.AbortOnConnectFail = bool.Parse(configuration["Redis:AbortOnConnectFail"] ?? "false");
                options.ConnectTimeout = int.Parse(configuration["Redis:ConnectTimeout"] ?? "5000");
                options.SyncTimeout = int.Parse(configuration["Redis:SyncTimeout"] ?? "5000");
                options.KeepAlive = int.Parse(configuration["Redis:KeepAlive"] ?? "60");
                options.ConnectRetry = int.Parse(configuration["Redis:ConnectRetry"] ?? "3");

                var multiplexer = ConnectionMultiplexer.Connect(options);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }

            // -------------------------
            // 3. Supabase (optional)
            // -------------------------
            var supabaseUrl = configuration["Supabase:Url"];
            var supabaseKey = configuration["Supabase:Key"];
            if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
            {
                var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey);
                supabaseClient.InitializeAsync().GetAwaiter().GetResult();
                services.AddSingleton(supabaseClient);
            }
        }
    }
}
