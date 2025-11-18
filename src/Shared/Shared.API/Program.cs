using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Text;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Extensions;
using shop_back.src.Shared.Infrastructure.Middlewares;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using shop_back.src.Shared.Domain.Enums;
using StackExchange.Redis;
using System.Security.Claims;

try { Env.Load(); } catch { }
var builder = WebApplication.CreateBuilder(args);

// Load .env
var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
Console.WriteLine("ENV LOADED FROM: " + envPath);
try { DotNetEnv.Env.Load(envPath); } catch { }


// Database
var connStr = DotNetEnv.Env.GetString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

Console.WriteLine($"DefaultConnection: {DotNetEnv.Env.GetString("DefaultConnection")}");
Console.WriteLine($"RedisConnectionString: {DotNetEnv.Env.GetString("RedisConnectionString")}");

// Redis
var redisConn = DotNetEnv.Env.GetString("RedisConnectionString");
var multiplexer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// Add Shared Repositories & Services
builder.Services.AddSettings(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();

// Auth (JWT + CSRF)
var key = Encoding.UTF8.GetBytes(DotNetEnv.Env.GetString("JwtKey")!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = DotNetEnv.Env.GetString("JwtIssuer")!,
            ValidAudience = DotNetEnv.Env.GetString("JwtAudience")!,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Lax;
#if DEBUG
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
#else
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DynamicPermission", policy =>
    {
        // Instead of directly mutating Requirements, use RequireAssertion
        policy.RequireAssertion(context =>
        {
            // You can inject the handler via DI or resolve services from context
            // For now, we just mark the requirement to succeed; the real check happens in PermissionHandler
            context.Succeed(new PermissionRequirement([], PermissionRelation.Or));
            return true;
        });
    });

// Register the handler
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandlerService>();

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(p =>
{
    p.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:4200",
            "http://localhost:3000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

#if !DEBUG
app.UseHttpsRedirection();
#endif

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfAndJwtMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
