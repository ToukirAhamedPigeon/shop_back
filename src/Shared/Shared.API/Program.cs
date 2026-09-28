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
using shop_back.src.Shared.Infrastructure.Services;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using shop_back.src.Shared.Infrastructure.Helpers;
using System.Net.Sockets;
using System.Diagnostics;

// Load .env file at the very beginning
try
{
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

builder.Configuration.AddEnvironmentVariables();

// ------------------- FILE UPLOAD SIZE LIMITS -------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

// ------------------- REDIS AUTO-START FUNCTIONS -------------------
static (string host, int port) ParseRedisConnectionString(string redisConn)
{
    var parts = redisConn.Split(',');
    var hostPort = parts[0].Trim();
    var hostParts = hostPort.Split(':');

    var host = hostParts.Length > 0 ? hostParts[0] : "localhost";
    var port = hostParts.Length > 1 ? int.Parse(hostParts[1]) : 6379;

    return (host, port);
}

static bool IsRedisRunning(string host, int port, int timeoutMs = 1000)
{
    try
    {
        using var client = new TcpClient();
        var result = client.BeginConnect(host, port, null, null);
        var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
        if (success)
        {
            client.EndConnect(result);
            return true;
        }
        return false;
    }
    catch
    {
        return false;
    }
}

static Process? StartRedisProcess(string redisPath, string? configPath = null)
{
    try
    {
        Console.WriteLine($"🔄 Starting Redis from: {redisPath}");

        var arguments = configPath != null ? $"{configPath}" : "";

        var startInfo = new ProcessStartInfo
        {
            FileName = redisPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(startInfo);
        if (process != null)
        {
            Console.WriteLine($"✅ Redis process started successfully (PID: {process.Id})");
            return process;
        }
        return null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to start Redis: {ex.Message}");
        return null;
    }
}

static string? FindRedisExecutable()
{
    // Common Redis installation paths
    var redisPaths = new[]
    {
        @"C:\Program Files\Redis\redis-server.exe",
        @"C:\Redis\redis-server.exe",
        @"C:\ProgramData\chocolatey\bin\redis-server.exe",
        @"C:\Program Files\Microsoft\Redis\redis-server.exe",
        @"redis-server.exe" // In PATH
    };

    foreach (var path in redisPaths)
    {
        if (File.Exists(path))
        {
            return path;
        }
    }

    // Try to find via where command
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "where",
            Arguments = "redis-server",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            process.WaitForExit(1000);
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(output) && File.Exists(output))
            {
                return output;
            }
        }
    }
    catch { }

    return null;
}

static bool EnsureRedisRunning(string redisConn, int maxRetries = 15)
{
    try
    {
        var (host, port) = ParseRedisConnectionString(redisConn);

        // Check if Redis is already running
        Console.WriteLine($"🔍 Checking if Redis is running on {host}:{port}...");
        if (IsRedisRunning(host, port, 1000))
        {
            Console.WriteLine($"✅ Redis already running on {host}:{port}");
            return true;
        }

        Console.WriteLine($"⚠️ Redis not running on {host}:{port}. Attempting to start...");

        // Find Redis executable
        var redisPath = FindRedisExecutable();
        if (string.IsNullOrEmpty(redisPath))
        {
            Console.WriteLine("❌ Redis executable not found!");
            Console.WriteLine("   Please install Redis using one of these methods:");
            Console.WriteLine("   1. Windows: choco install redis-64");
            Console.WriteLine("   2. Download from: https://github.com/microsoftarchive/redis/releases");
            Console.WriteLine("   3. Docker: docker run -d -p 6379:6379 --name redis redis:alpine");
            return false;
        }

        // Try to start Redis as a service first (if installed as service)
        try
        {
            var serviceStartInfo = new ProcessStartInfo
            {
                FileName = redisPath,
                Arguments = "--service-start",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var serviceProcess = Process.Start(serviceStartInfo);
            if (serviceProcess != null)
            {
                serviceProcess.WaitForExit(3000);
                if (serviceProcess.ExitCode == 0)
                {
                    Console.WriteLine("✅ Redis service started successfully");
                    // Wait for Redis to be ready
                    for (int i = 1; i <= maxRetries; i++)
                    {
                        Thread.Sleep(1000);
                        if (IsRedisRunning(host, port, 500))
                        {
                            Console.WriteLine($"✅ Redis is now ready after {i} second(s)");
                            return true;
                        }
                        Console.WriteLine($"   Still waiting... ({i}/{maxRetries})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not start Redis as service: {ex.Message}");
        }

        // If service start failed, start Redis as a direct process
        Console.WriteLine("🔄 Starting Redis as a direct process...");
        var process = StartRedisProcess(redisPath);
        if (process != null)
        {
            Console.WriteLine($"⏳ Waiting for Redis to be ready...");
            for (int i = 1; i <= maxRetries; i++)
            {
                Thread.Sleep(1000);
                if (IsRedisRunning(host, port, 500))
                {
                    Console.WriteLine($"✅ Redis is now ready after {i} second(s)");
                    // Keep the process alive by not disposing it
                    // We'll let it run in the background
                    return true;
                }
                Console.WriteLine($"   Still waiting... ({i}/{maxRetries})");
            }
        }

        Console.WriteLine($"❌ Redis did not become ready after {maxRetries} seconds");
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error ensuring Redis is running: {ex.Message}");
        return false;
    }
}

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

// ------------------- REDIS CONNECTION -------------------
var redisConn = Env.GetString("RedisConnectionString");
if (string.IsNullOrEmpty(redisConn))
{
    redisConn = builder.Configuration["RedisConnectionString"] ?? "localhost:6379";
}

// Ensure Redis is running before attempting to connect
Console.WriteLine("🚀 Starting Redis server automatically...");
var redisReady = EnsureRedisRunning(redisConn, maxRetries: 20);

// Build the full connection string with options
var fullRedisConn = redisConn;
if (!fullRedisConn.Contains("abortConnect="))
{
    fullRedisConn += ",abortConnect=false,connectTimeout=10000,responseTimeout=10000,connectRetry=3";
}

Console.WriteLine($"🔗 Redis Connection String: {fullRedisConn}");

// Try to connect with retry
IConnectionMultiplexer? multiplexer = null;
if (redisReady)
{
    for (int retry = 0; retry < 5; retry++)
    {
        try
        {
            Console.WriteLine($"🔄 Connecting to Redis (attempt {retry + 1}/5)...");
            var configOptions = ConfigurationOptions.Parse(fullRedisConn);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 10000;
            configOptions.SyncTimeout = 10000;
            configOptions.ConnectRetry = 3;

            multiplexer = ConnectionMultiplexer.Connect(configOptions);

            // Verify connection
            if (multiplexer.IsConnected)
            {
                var db = multiplexer.GetDatabase();
                db.Ping();
                Console.WriteLine("✅ Redis connected successfully!");
                builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
                break;
            }
            else
            {
                Console.WriteLine("⚠️ Redis connected but not in connected state");
                multiplexer.Dispose();
                multiplexer = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Redis connection attempt {retry + 1} failed: {ex.Message}");
            if (retry < 4)
            {
                var delay = 2000 * (retry + 1);
                Console.WriteLine($"   Retrying in {delay / 1000} seconds...");
                Thread.Sleep(delay);
            }
        }
    }
}

if (multiplexer == null || !multiplexer.IsConnected)
{
    Console.WriteLine("⚠️⚠️⚠️ Redis connection failed after all attempts.");
    Console.WriteLine("   Continuing with fallback mode (Redis features will be disabled).");
    Console.WriteLine("   To fix this issue:");
    Console.WriteLine("   1. Check if Redis is installed: redis-cli --version");
    Console.WriteLine("   2. Try starting Redis manually: redis-server");
    Console.WriteLine("   3. Verify Redis is running: redis-cli ping");
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

builder.Services.AddSingleton<RemoteFileHelper>();

// ------------------- GOOGLE DRIVE CREDENTIALS PATH -------------------
var googleDriveCredentialsPath = Env.GetString("GOOGLE_DRIVE_CREDENTIALS_PATH")
    ?? builder.Configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"]
    ?? "oauth-credentials.json";

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
        googleDriveCredentialsPath = resolvedPath;
        Console.WriteLine($"⚠️ Google Drive credentials not found at: {resolvedPath}");
    }
}
else
{
    if (File.Exists(googleDriveCredentialsPath))
        Console.WriteLine($"✅ Found Google Drive credentials at: {googleDriveCredentialsPath}");
    else
        Console.WriteLine($"⚠️ Google Drive credentials not found at: {googleDriveCredentialsPath}");
}

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

// ------------------- PERMISSION GROUP TABLES -------------------
// Idempotent; creates the tables on databases that predate the feature.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await PermissionGroupSchema.EnsureAsync(db);
    Console.WriteLine("✅ Permission group tables ready");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Could not prepare permission group tables: {ex.Message}");
}

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
Console.WriteLine("==========================================");
Console.WriteLine("=== SERVER STARTUP COMPLETE ===");
Console.WriteLine($"Environment: {(builder.Environment.IsDevelopment() ? "Development" : "Production")}");
Console.WriteLine($"Google Drive Credentials Path: {builder.Configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"]}");
Console.WriteLine($"Google Drive Folder ID: {builder.Configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"]}");
Console.WriteLine($"File Storage Type: {builder.Configuration["FILE_STORAGE_TYPE"]}");
Console.WriteLine($"Remote Storage URL: {builder.Configuration["REMOTE_STORAGE_URL"]}");
Console.WriteLine($"Redis Status: {(multiplexer != null && multiplexer.IsConnected ? "✅ Connected" : "⚠️ Fallback Mode (Redis not available)")}");
Console.WriteLine("==========================================");

app.Run();