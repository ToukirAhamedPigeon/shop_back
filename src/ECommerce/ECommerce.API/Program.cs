using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Text;

// Namespace aliases
using SharedExt = shop_back.src.Shared.Infrastructure.Extensions;
using ECommerceExt = shop_back.src.ECommerce.Infrastructure.Extensions;

// Shared Infrastructure
using shop_back.src.Shared.Infrastructure.Data;

// ECommerce Infrastructure
using shop_back.src.ECommerce.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// 1. Shared Services (DB, Redis, Supabase)
// --------------------
SharedExt.SharedServiceExtensions.AddSharedServices(builder.Services, builder.Configuration);

// --------------------
// 2. ECommerce DB Context
// --------------------
// Use the same DefaultConnection
var defaultConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? throw new InvalidOperationException("DefaultConnection not set");

builder.Services.AddDbContext<ECommerceDbContext>(options =>
    options.UseNpgsql(defaultConnStr));

// --------------------
// 3. Register Repositories & Services
// --------------------
SharedExt.RepositoryServiceExtensions.AddRepositories(builder.Services);
SharedExt.ApplicationServiceExtensions.AddServices(builder.Services);

ECommerceExt.RepositoryServiceExtensions.AddRepositories(builder.Services);
ECommerceExt.ApplicationServiceExtensions.AddServices(builder.Services);

// --------------------
// 4. Authentication & Authorization
// --------------------
var jwtKey = builder.Configuration["JwtKey"]
             ?? throw new InvalidOperationException("JwtKey not set");

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtIssuer"],
            ValidAudience = builder.Configuration["JwtAudience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
    });

builder.Services.AddAuthorization();

// --------------------
// 5. Controllers & Swagger
// --------------------
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// --------------------
// Build App
// --------------------
var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECommerce.API V1");
});

app.MapControllers();
app.Run();
