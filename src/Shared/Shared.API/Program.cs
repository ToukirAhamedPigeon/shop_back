using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

try { Env.Load(); } catch { }

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ------------------- LOAD .ENV -------------------
var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
try { Env.Load(envPath); } catch { }

// ------------------- DATABASE -------------------
var connStr = Env.GetString("DefaultConnection");
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
// Register as Singleton instead of Scoped
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["FILE_STORAGE_TYPE"] = Env.GetString("FILE_STORAGE_TYPE") ?? "remote",
    ["REMOTE_STORAGE_URL"] = Env.GetString("REMOTE_STORAGE_URL") ?? "https://shopfiles.pigeonic.com",
    ["REMOTE_STORAGE_TOKEN"] = Env.GetString("REMOTE_STORAGE_TOKEN") ?? ""
});

// ডিবাগ
Console.WriteLine($"After manual add - FILE_STORAGE_TYPE: {builder.Configuration["FILE_STORAGE_TYPE"]}");
builder.Services.AddSingleton<RemoteFileHelper>();

// ------------------- JWT AUTHENTICATION -------------------
var key = Encoding.UTF8.GetBytes(Env.GetString("JwtKey")!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev-safe
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

// ------------------- AUTHORIZATION (🔥 IMPORTANT) -------------------
builder.Services.AddAuthorization();

// 🔑 Dynamic permission system
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
            .AllowCredentials();
    });
});
// কনফিগারেশন চেক করুন
var fileStorageType = builder.Configuration["FILE_STORAGE_TYPE"];
var remoteUrl = builder.Configuration["REMOTE_STORAGE_URL"];
Console.WriteLine($"=== CONFIGURATION CHECK ===");
Console.WriteLine($"FILE_STORAGE_TYPE from config: '{fileStorageType}'");
Console.WriteLine($"REMOTE_STORAGE_URL from config: '{remoteUrl}'");

// এনভায়রনমেন্ট ভেরিয়েবল থেকেও চেক করুন
var envStorageType = Environment.GetEnvironmentVariable("FILE_STORAGE_TYPE");
Console.WriteLine($"FILE_STORAGE_TYPE from env: '{envStorageType}'");
var app = builder.Build();

// ------------------- INITIALIZE FILE HELPER WITH REMOTE STORAGE -------------------
// Get the service directly (now works because it's Singleton)
var remoteFileHelper = app.Services.GetRequiredService<RemoteFileHelper>();
FileHelper.Initialize(remoteFileHelper);

#if !DEBUG
app.UseHttpsRedirection();
#endif

app.UseRouting();
app.UseCors("AllowFrontend");

// ------------------- 🔐 AUTH PIPELINE (ORDER MATTERS) -------------------
app.UseAuthentication();
app.UseAuthorization();

// CSRF middleware should NOT interfere with auth
app.UseMiddleware<CsrfAndJwtMiddleware>();

// ------------------- OTHER -------------------
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();