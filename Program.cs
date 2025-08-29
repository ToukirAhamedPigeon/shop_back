using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Text;
using shop_back.App.Data;
using shop_back.App.Extensions;
using shop_back.App.Middlewares;
using shop_back.App.Authorization;
using StackExchange.Redis;

try { Env.Load(".env.local"); } catch { }

var builder = WebApplication.CreateBuilder(args);

// Database
string connStr = Environment.GetEnvironmentVariable("DefaultConnection") 
                 ?? Env.GetString("DefaultConnection");
builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Read Redis configuration safely
var redisConnString = builder.Configuration.GetValue<string>("Redis:ConnectionString")
                     ?? Env.GetString("Redis:ConnectionString");

if (string.IsNullOrEmpty(redisConnString))
    throw new InvalidOperationException("Redis connection string is not configured.");

// Optional: parse more settings if needed
var redisOptions = ConfigurationOptions.Parse(redisConnString);
redisOptions.Password = builder.Configuration.GetValue<string>("Redis:Password"); // optional
redisOptions.DefaultDatabase = builder.Configuration.GetValue<int?>("Redis:Database") ?? 0;
redisOptions.AbortOnConnectFail = builder.Configuration.GetValue<bool?>("Redis:AbortOnConnectFail") ?? false;
redisOptions.ConnectTimeout = builder.Configuration.GetValue<int?>("Redis:ConnectTimeout") ?? 5000;
redisOptions.SyncTimeout = builder.Configuration.GetValue<int?>("Redis:SyncTimeout") ?? 5000;
redisOptions.KeepAlive = builder.Configuration.GetValue<int?>("Redis:KeepAlive") ?? 60;
redisOptions.ConnectRetry = builder.Configuration.GetValue<int?>("Redis:ConnectRetry") ?? 3;

// Connect
var multiplexer = ConnectionMultiplexer.Connect(redisOptions);

// Register singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

Console.WriteLine("✅ Redis Connected: " + multiplexer.IsConnected);

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

// 🔑 Add Authorization with custom permission handler
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DynamicPermission", policy =>
        policy.Requirements.Add(new PermissionRequirement(new List<string>(), PermissionRelation.Or))
    );
});

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddHttpContextAccessor();

// Swagger
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
app.UseMiddleware<CsrfAndJwtMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// DB Test
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("✅ Connected to DB: " + db.Database.CanConnect());
}

app.Run();
