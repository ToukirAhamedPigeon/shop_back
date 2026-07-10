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
using shop_back.src.Shared.Infrastructure.Services;   // <-- ADD THIS for BackupSchedulerService
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using shop_back.src.Shared.Infrastructure.Helpers;

// Load .env file at the very beginning
try
{
    // Try multiple possible locations for .env
    var possiblePaths = new[]
    {
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env")),
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".env")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")),
    };

    var loaded = false;
    foreach (var envPath in possiblePaths)
    {
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            Console.WriteLine($"✅ Loaded .env from: {envPath}");
            loaded = true;
            break;
        }
    }

    if (!loaded)
    {
        Console.WriteLine($"⚠️ .env file not found in any of these locations:");
        foreach (var path in possiblePaths)
        {
            Console.WriteLine($"   - {path}");
        }
        Console.WriteLine("   Using environment variables or defaults.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to load .env: {ex.Message}");
    Console.WriteLine("   Continuing with environment variables or defaults.");
}

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ------------------- LOAD .ENV AGAIN FOR SAFETY -------------------
// Load .env into configuration
try
{
    var envPathAgain = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
    if (File.Exists(envPathAgain))
    {
        Env.Load(envPathAgain);
        Console.WriteLine($"✅ Reloaded .env from: {envPathAgain}");
    }
}
catch { }

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

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
if (string.IsNullOrEmpty(connStr))
{
    connStr = builder.Configuration.GetConnectionString("DefaultConnection") 
              ?? builder.Configuration["DefaultConnection"];
}

if (string.IsNullOrEmpty(connStr))
{
    Console.WriteLine($"❌ ERROR: DefaultConnection is not set!");
    Console.WriteLine("   Please ensure .env file contains: DefaultConnection=your-connection-string");
}
else
{
    // Log connection string (mask sensitive data)
    var logConn = connStr;
    if (logConn.Contains("Password="))
    {
        var passwordMatch = System.Text.RegularExpressions.Regex.Match(logConn, @"Password=([^;]+)");
        if (passwordMatch.Success)
        {
            logConn = logConn.Replace(passwordMatch.Value, "Password=*****");
        }
    }
    Console.WriteLine($"🔗 Database Connection String: {logConn}");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// ------------------- REDIS -------------------
var redisConn = Env.GetString("RedisConnectionString");
if (string.IsNullOrEmpty(redisConn))
{
    redisConn = builder.Configuration["RedisConnectionString"] ?? "localhost:6379";
}
Console.WriteLine($"🔗 Redis Connection: {(string.IsNullOrEmpty(redisConn) ? "NOT FOUND!" : redisConn)}");

try
{
    var multiplexer = ConnectionMultiplexer.Connect(redisConn);
    builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    Console.WriteLine("✅ Redis connected successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Redis connection failed: {ex.Message}");
}

// ------------------- HTTP CLIENT FOR REMOTE STORAGE -------------------
builder.Services.AddHttpClient();

// ------------------- REPOSITORIES & SERVICES -------------------
builder.Services.AddSettings(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();

// Register the background scheduler
builder.Services.AddHostedService<BackupSchedulerService>();

// ------------------- FILE STORAGE HELPERS -------------------
var fileStorageType = Env.GetString("FILE_STORAGE_TYPE") ?? builder.Configuration["FILE_STORAGE_TYPE"] ?? "remote";
var remoteStorageUrl = Env.GetString("REMOTE_STORAGE_URL") ?? builder.Configuration["REMOTE_STORAGE_URL"] ?? "https://shopfiles.pigeonic.com";
var remoteStorageToken = Env.GetString("REMOTE_STORAGE_TOKEN") ?? builder.Configuration["REMOTE_STORAGE_TOKEN"] ?? "";

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["FILE_STORAGE_TYPE"] = fileStorageType,
    ["REMOTE_STORAGE_URL"] = remoteStorageUrl,
    ["REMOTE_STORAGE_TOKEN"] = remoteStorageToken
});

Console.WriteLine($"=== CONFIGURATION CHECK ===");
Console.WriteLine($"FILE_STORAGE_TYPE: '{fileStorageType}'");
Console.WriteLine($"REMOTE_STORAGE_URL: '{remoteStorageUrl}'");
Console.WriteLine($"REMOTE_STORAGE_TOKEN: {(string.IsNullOrEmpty(remoteStorageToken) ? "NOT SET" : "SET ✓")}");

// Register RemoteFileHelper
builder.Services.AddSingleton<RemoteFileHelper>();

// ------------------- GOOGLE DRIVE CREDENTIALS PATH -------------------
var googleDriveCredentialsPath = Env.GetString("GOOGLE_DRIVE_CREDENTIALS_PATH") 
    ?? builder.Configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"] 
    ?? "oauth-credentials.json"; // default to OAuth2 file

// Resolve relative path if needed
if (!Path.IsPathRooted(googleDriveCredentialsPath))
{
    var apiProjectPath = Directory.GetCurrentDirectory();
    var resolvedPath = Path.Combine(apiProjectPath, googleDriveCredentialsPath);
    if (File.Exists(resolvedPath))
    {
        googleDriveCredentialsPath = resolvedPath;
        Console.WriteLine($"✅ Found Google Drive credentials at: {resolvedPath}");
    }
    else
    {
        // Use the resolved path even if not found (the service will handle it)
        googleDriveCredentialsPath = resolvedPath;
        Console.WriteLine($"⚠️ Google Drive credentials not found at: {resolvedPath}, will attempt to use this path.");
    }
}
else
{
    if (File.Exists(googleDriveCredentialsPath))
        Console.WriteLine($"✅ Found Google Drive credentials at: {googleDriveCredentialsPath}");
    else
        Console.WriteLine($"⚠️ Google Drive credentials not found at: {googleDriveCredentialsPath}");
}

// Add to configuration
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["GOOGLE_DRIVE_CREDENTIALS_PATH"] = googleDriveCredentialsPath,
    ["GOOGLE_DRIVE_BACKUP_FOLDER_ID"] = Env.GetString("GOOGLE_DRIVE_BACKUP_FOLDER_ID") 
        ?? builder.Configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"] 
        ?? ""
});

Console.WriteLine($"📁 Google Drive Credentials Path: {googleDriveCredentialsPath}");
Console.WriteLine($"📁 Google Drive Folder ID: {builder.Configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"]}");

// ------------------- JWT AUTHENTICATION -------------------
var jwtKey = Env.GetString("JwtKey") ?? builder.Configuration["JwtKey"];
if (string.IsNullOrEmpty(jwtKey))
{
    Console.WriteLine($"❌ ERROR: JwtKey is not set!");
    Console.WriteLine("   Please ensure .env file contains: JwtKey=your-secret-key");
    // Set a default for development (not secure for production!)
    jwtKey = "dev-secret-key-do-not-use-in-production-1234567890";
}

var jwtIssuer = Env.GetString("JwtIssuer") ?? builder.Configuration["JwtIssuer"] ?? "shopsphere";
var jwtAudience = Env.GetString("JwtAudience") ?? builder.Configuration["JwtAudience"] ?? "shopsphere";

var key = Encoding.UTF8.GetBytes(jwtKey);

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
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

var app = builder.Build();

// ------------------- INITIALIZE FILE HELPER -------------------
try
{
    var remoteFileHelper = app.Services.GetRequiredService<RemoteFileHelper>();
    var webHostEnvironment = app.Services.GetRequiredService<IWebHostEnvironment>();
    FileHelper.Initialize(remoteFileHelper, webHostEnvironment);
    Console.WriteLine("✅ FileHelper initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize FileHelper: {ex.Message}");
}

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

// ------------------- LOG STARTUP CONFIGURATION -------------------
Console.WriteLine("=== SERVER STARTUP COMPLETE ===");
Console.WriteLine($"Environment: {(builder.Environment.IsDevelopment() ? "Development" : "Production")}");
Console.WriteLine($"Google Drive Credentials Path: {builder.Configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"]}");
Console.WriteLine($"Google Drive Folder ID: {builder.Configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"]}");
Console.WriteLine($"File Storage Type: {builder.Configuration["FILE_STORAGE_TYPE"]}");
Console.WriteLine($"Remote Storage URL: {builder.Configuration["REMOTE_STORAGE_URL"]}");
Console.WriteLine("==================================");

app.Run();