using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Text;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Extensions;
using shop_back.src.Shared.Infrastructure.Middlewares;
using shop_back.src.Shared.Application.Authorization;
using shop_back.src.Shared.Domain.Enums;
using shop_back.src.Shared.Application;
using StackExchange.Redis;

try { Env.Load(); } catch { }
var builder = WebApplication.CreateBuilder(args);

// Load .env
try { DotNetEnv.Env.Load(".env.local"); } catch { }

// Database
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") 
              ?? DotNetEnv.Env.GetString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// Redis
var redisConn = builder.Configuration.GetValue<string>("Redis:ConnectionString") 
                ?? DotNetEnv.Env.GetString("Redis:ConnectionString");
var multiplexer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// Add Shared Repositories & Services
builder.Services.AddRepositories();
builder.Services.AddServices();

// Auth (JWT + CSRF)
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "XSRF-TOKEN";
    o.Cookie.HttpOnly = false;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.None;
});

// Authorization Policies
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("DynamicPermission", p =>
        p.Requirements.Add(new PermissionRequirement(new List<string>(), PermissionRelation.Or)));
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(p =>
{
    p.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfAndJwtMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
