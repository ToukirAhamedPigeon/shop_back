using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Text;
using shop_back.App.Data;
using shop_back.App.Extensions;
using shop_back.App.Middlewares;

try { Env.Load(".env.local"); } catch { }

// Build WebApplication
var builder = WebApplication.CreateBuilder(args);

// Database
string connStr = Environment.GetEnvironmentVariable("DefaultConnection") 
                 ?? Env.GetString("DefaultConnection");
builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories & Services
builder.Services.AddRepositories();
builder.Services.AddServices();

// Controllers
builder.Services.AddControllers();

// JWT Authentication
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

// CSRF / XSRF Protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // SPA sends this header
    options.Cookie.Name = "XSRF-TOKEN";   // JS-readable cookie
    options.Cookie.HttpOnly = false;      // JS can read it
    options.Cookie.SameSite = SameSiteMode.Lax;
#if DEBUG
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
#else
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
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

#if !DEBUG
app.UseHttpsRedirection();
#endif

app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// CSRF + JWT Middleware
// Ensure the middleware skips CSRF for endpoints with JWT or AllowAnonymous
app.UseMiddleware<CsrfAndJwtMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Test DB Connection
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("✅ Connected to DB: " + db.Database.CanConnect());
}

app.Run();
