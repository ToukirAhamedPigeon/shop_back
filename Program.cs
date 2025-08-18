using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DotNetEnv; // For loading variables from .env.local
using Microsoft.EntityFrameworkCore; // For EF Core
using shop_back.App.Data;
using shop_back.App.Extensions;
using shop_back.App.Models;

// Load .env.local only if it exists (for local development)
try
{
    Env.Load(".env.local");
}
catch
{
    // .env.local might not exist in Docker, ignore if not found
}

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Get connection string from environment variable or .env.local
string connStr = Environment.GetEnvironmentVariable("DefaultConnection") ?? Env.GetString("DefaultConnection");

if (!string.IsNullOrEmpty(connStr))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
}
else
{
    Console.WriteLine("⚠️ Warning: DefaultConnection string not set in environment or .env.local");
}

// Load allowed CORS origins from config or fallback to empty array
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// Register DbContext with PostgreSQL using connection string
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repository and service layers
builder.Services.AddRepositories();
builder.Services.AddServices();

// Add API controllers
builder.Services.AddControllers();

// JWT Config
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

// Enable Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Define CORS policy named "AllowFrontend"
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

var app = builder.Build();

// Test DB connection on startup (optional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("✅ Connected to DB: " + db.Database.CanConnect());
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// Routing must come before middleware like CORS and Authorization
app.UseRouting();

// Apply CORS middleware BEFORE Authorization
app.UseCors("AllowFrontend");

// Authorization middleware
app.UseAuthorization();

// Auth Seeder
// using (var scope = app.Services.CreateScope())
// {
//     var services = scope.ServiceProvider;
//     try
//     {
//         await InitAuthSeeder.Seed(services);
//         Console.WriteLine("Authentication seed completed.");
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"Seeding failed: {ex.Message}");
//     }
// }

// Map controller routes (API endpoints)
app.MapControllers();

// Run the application
app.Run();
