// src/Shared/Shared.API/Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Text;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Extensions;
using shop_back.src.Shared.Infrastructure.Middlewares;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using shop_back.src.Shared.Infrastructure.Helpers;

// Load .env file at the very beginning
try
{
    var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        Console.WriteLine($"✅ Loaded .env from: {envPath}");
    }
    else
    {
        // Try current directory
        var currentEnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(currentEnvPath))
        {
            Env.Load(currentEnvPath);
            Console.WriteLine($"✅ Loaded .env from: {currentEnvPath}");
        }
        else
        {
            Console.WriteLine($"⚠️ .env file not found at: {envPath} or {currentEnvPath}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to load .env: {ex.Message}");
}

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ------------------- LOAD .ENV AGAIN FOR SAFETY -------------------
var envPathAgain = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
try { Env.Load(envPathAgain); } catch { }

// ------------------- FILE UPLOAD SIZE LIMITS -------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB limit
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200MB
});

// ------------------- DATABASE -------------------
var connStr = Env.GetString("DefaultConnection");
Console.WriteLine($"🔗 Database Connection String: {(string.IsNullOrEmpty(connStr) ? "NOT FOUND!" : "Loaded")}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// ------------------- REDIS -------------------
var redisConn = Env.GetString("RedisConnectionString");
var multiplexer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// ------------------- HTTP CLIENT FOR REMOTE STORAGE -------------------
builder.Services.AddHttpClient();

// ------------------- REPOSITORIES & SERVICES -------------------
builder.Services.AddSettings(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();

// ------------------- FILE STORAGE HELPERS -------------------
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["FILE_STORAGE_TYPE"] = Env.GetString("FILE_STORAGE_TYPE") ?? "remote",
    ["REMOTE_STORAGE_URL"] = Env.GetString("REMOTE_STORAGE_URL") ?? "https://shopfiles.pigeonic.com",
    ["REMOTE_STORAGE_TOKEN"] = Env.GetString("REMOTE_STORAGE_TOKEN") ?? ""
});

Console.WriteLine($"FILE_STORAGE_TYPE: {builder.Configuration["FILE_STORAGE_TYPE"]}");
builder.Services.AddSingleton<RemoteFileHelper>();

// ------------------- JWT AUTHENTICATION -------------------
var key = Encoding.UTF8.GetBytes(Env.GetString("JwtKey")!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Env.GetString("JwtIssuer"),
            ValidAudience = Env.GetString("JwtAudience"),
            IssuerSigningKey = new SymmetricSecurityKey(key),
        };
    });

// ------------------- AUTHORIZATION -------------------
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandlerService>();

// ------------------- CSRF -------------------
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

// ------------------- CONTROLLERS, SWAGGER, CORS -------------------
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
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition");
    });
});

var fileStorageType = builder.Configuration["FILE_STORAGE_TYPE"];
var remoteUrl = builder.Configuration["REMOTE_STORAGE_URL"];
Console.WriteLine($"=== CONFIGURATION CHECK ===");
Console.WriteLine($"FILE_STORAGE_TYPE from config: '{fileStorageType}'");
Console.WriteLine($"REMOTE_STORAGE_URL from config: '{remoteUrl}'");

var app = builder.Build();

// ------------------- INITIALIZE FILE HELPER -------------------
var remoteFileHelper = app.Services.GetRequiredService<RemoteFileHelper>();
var webHostEnvironment = app.Services.GetRequiredService<IWebHostEnvironment>();
FileHelper.Initialize(remoteFileHelper, webHostEnvironment);

#if !DEBUG
app.UseHttpsRedirection();
#endif

app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<CsrfAndJwtMiddleware>();

app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();