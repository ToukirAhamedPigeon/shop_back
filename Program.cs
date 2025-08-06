using DotNetEnv; // For loading variables from .env.local
using Microsoft.EntityFrameworkCore; // For EF Core
using Microsoft.EntityFrameworkCore.Design;
using server.App.Data; // Replace with your actual namespace
using server.App.Extensions;
using server.App.Models;

Env.Load(".env.local"); // 🔐 Load secrets like DB connection string from .env.local

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// 💡 Inject the DB connection string from the environment into the config system
builder.Configuration["ConnectionStrings:DefaultConnection"] = Env.GetString("DefaultConnection");

// 🔌 Get the connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 🌐 Load allowed CORS origins (e.g., frontend URL) from appsettings.json or elsewhere
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// 📦 Register the EF Core DB context with PostgreSQL and Supabase connection string
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));



// 🧱 Register repository patterns
builder.Services.AddRepositories();

// 🛠 Register business logic layer (services)
builder.Services.AddServices();

// 🚀 Add support for API controllers
builder.Services.AddControllers();

// 🔍 Enable Swagger for API documentation (OpenAPI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔐 Define a CORS policy named "AllowFrontend"
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins) // Set allowed frontend origins (from config)
              .AllowAnyHeader()            // Allow all headers
              .AllowAnyMethod()            // Allow all HTTP methods (GET, POST, etc.)
              .AllowCredentials();         // Support cookies, tokens, or credentials
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("✅ Connected to DB: " + db.Database.CanConnect());
}

// 🧪 Enable Swagger UI in development environment
app.UseSwagger();
app.UseSwaggerUI();

// 🚦 Set up the HTTP request pipeline

app.UseRouting();           // 📍 Enable routing for controller/action matching
app.UseCors("AllowFrontend"); // 🔓 Apply the defined CORS policy
app.UseAuthorization();     // 🔐 Enable authorization middleware (if using auth)

// 📬 Map controller routes (e.g., /api/products)
app.MapControllers();

// ▶️ Start the app
app.Run();
