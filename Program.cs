using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using DotNetEnv; // For loading environment variables
using System.Text;
using shop_back.App.Data;
using shop_back.App.Extensions;
using shop_back.App.Middlewares; // CsrfValidationMiddleware
using shop_back.App.Models;

try
{
    // Load .env.local for local development (ignored in production/Docker)
    Env.Load(".env.local");
}
catch
{
    // .env.local might not exist, ignore
}

// Build the WebApplication
var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// Kestrel Configuration
// ----------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Listen on port 5000
});

// ----------------------------
// Database Configuration
// ----------------------------
string connStr = Environment.GetEnvironmentVariable("DefaultConnection") 
                 ?? Env.GetString("DefaultConnection");

if (!string.IsNullOrEmpty(connStr))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
}
else
{
    Console.WriteLine("⚠️ Warning: DefaultConnection string not set");
}

// Register EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ----------------------------
// Repositories and Services
// ----------------------------
builder.Services.AddRepositories();
builder.Services.AddServices();

// ----------------------------
// Controllers
// ----------------------------
builder.Services.AddControllers();

// ----------------------------
// JWT Authentication
// ----------------------------
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// ----------------------------
// CSRF Protection
// ----------------------------
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // SPA sends this header
    options.Cookie.Name = "X-CSRF-COOKIE"; // HttpOnly cookie
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict; // Prevent cross-site
    options.SuppressXFrameOptionsHeader = false;
});

// ----------------------------
// Swagger/OpenAPI
// ----------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ----------------------------
// CORS Policy
// ----------------------------
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://ec2-13-61-64-134.eu-north-1.compute.amazonaws.com:5173",
            "http://ec2-13-61-64-134.eu-north-1.compute.amazonaws.com:5174",
            "http://ec2-13-61-64-134.eu-north-1.compute.amazonaws.com:4200",
            "http://ec2-13-61-64-134.eu-north-1.compute.amazonaws.com:3000",
            "http://localhost:4200",
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:3000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// ----------------------------
// Build App
// ----------------------------
var app = builder.Build();

// ----------------------------
// Optional: Test DB Connection
// ----------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("✅ Connected to DB: " + db.Database.CanConnect());
}

// ----------------------------
// Middleware Pipeline
// ----------------------------

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// Routing
app.UseRouting();

// Apply CORS
app.UseCors("AllowFrontend");

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// ----------------------------
// CSRF Validation Middleware
// Must be added after Authentication and before Controllers
// ----------------------------
app.UseMiddleware<CsrfValidationMiddleware>();

// Map controller endpoints
app.MapControllers();

// Run application
app.Run();
