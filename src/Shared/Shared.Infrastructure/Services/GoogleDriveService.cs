// src/Shared/Shared.Infrastructure/Services/GoogleDriveService.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace shop_back.src.Shared.Application.Services
{
    public class GoogleDriveService : IGoogleDriveService
    {
        private DriveService? _driveService;
        private string? _backupFolderId;
        private readonly bool _isConfigured;
        private readonly IHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public GoogleDriveService(IConfiguration configuration, IHostEnvironment environment)
        {
            _environment = environment;
            _configuration = configuration;

            var credentialsType = configuration["GOOGLE_DRIVE_CREDENTIALS_TYPE"] ?? "serviceaccount";
            var credentialsPath = configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"] ?? "credentials.json";

            if (!Path.IsPathRooted(credentialsPath))
            {
                var possiblePaths = new List<string>
                {
                    Path.Combine(Directory.GetCurrentDirectory(), credentialsPath),
                    Path.Combine(Directory.GetCurrentDirectory(), "src", "Shared", "Shared.API", credentialsPath),
                    Path.Combine(_environment.ContentRootPath, credentialsPath),
                };
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        credentialsPath = path;
                        Console.WriteLine($"✅ Found credentials at: {credentialsPath}");
                        break;
                    }
                }
            }

            _backupFolderId = configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"];

            Console.WriteLine($"📁 Google Drive Config:");
            Console.WriteLine($"  Credentials Type: {credentialsType}");
            Console.WriteLine($"  Credentials Path: {credentialsPath}");
            Console.WriteLine($"  Folder ID: {_backupFolderId}");

            try
            {
                if (!System.IO.File.Exists(credentialsPath))
                {
                    Console.WriteLine($"❌ Credentials file not found: {credentialsPath}");
                    _isConfigured = false;
                    return;
                }

                if (credentialsType.Equals("oauth2", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("🔐 Using OAuth2 with user authorization (personal account)");
                    InitializeOAuth2(credentialsPath);
                }
                else
                {
                    Console.WriteLine("🔐 Using Service Account credentials");
                    InitializeServiceAccount(credentialsPath);
                }

                if (_driveService != null)
                {
                    _isConfigured = true;
                    Console.WriteLine("✅ Google Drive Service configured successfully.");

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var testResult = await TestConnectionAsync();
                            Console.WriteLine($"🔍 Initial connection test: {(testResult ? "✅ Success" : "❌ Failed")}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Initial connection test failed: {ex.Message}");
                        }
                    });
                }
                else
                {
                    _isConfigured = false;
                    Console.WriteLine("❌ DriveService could not be initialized.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to initialize Google Drive: {ex.Message}");
                _isConfigured = false;
            }
        }

        private void InitializeOAuth2(string credentialsPath)
        {
            try
            {
                using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
                var clientSecrets = GoogleClientSecrets.FromStream(stream).Secrets;

                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    new[] { DriveService.ScopeConstants.Drive },
                    "shopsphere_user",
                    CancellationToken.None,
                    new FileDataStore("GoogleDriveToken", true)
                ).Result;

                _driveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "ShopSphere Backup"
                });
            }
            catch (AggregateException ae)
            {
                var inner = ae.InnerException ?? ae;
                throw new Exception($"OAuth2 authorization failed: {inner.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"OAuth2 initialization failed: {ex.Message}");
            }
        }

        private void InitializeServiceAccount(string credentialsPath)
        {
#pragma warning disable CS0618 // Obsolete, but still works for now
            using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped(DriveService.ScopeConstants.Drive);
#pragma warning restore CS0618

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "ShopSphere Backup"
            });
        }

        // -----------------------------------------------------------------
        // Public methods
        // -----------------------------------------------------------------

        public async Task<string> UploadFileAsync(string filePath, string fileName, string folder)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                Console.WriteLine($"📤 Uploading to Google Drive...");
                Console.WriteLine($"  File: {filePath}");
                Console.WriteLine($"  FileName: {fileName}");
                Console.WriteLine($"  Folder ID: {_backupFolderId}");

                if (!string.IsNullOrEmpty(_backupFolderId))
                {
                    var folderCheck = await GetFolderAsync(_backupFolderId);
                    if (folderCheck == null)
                    {
                        Console.WriteLine($"⚠️ Folder {_backupFolderId} not found or not accessible");
                        Console.WriteLine("   Attempting to create folder...");
                        var newFolderId = await CreateFolderAsync("Backups");
                        if (!string.IsNullOrEmpty(newFolderId))
                        {
                            _backupFolderId = newFolderId;
                            Console.WriteLine($"✅ Created new folder with ID: {_backupFolderId}");
                        }
                        else
                        {
                            throw new Exception($"Cannot access folder. Please ensure the folder exists and is shared with the service account.");
                        }
                    }
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return await UploadFileFromStreamAsync(stream, fileName, folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload failed: {ex.Message}");
                throw new Exception($"Failed to upload file to Google Drive: {ex.Message}");
            }
        }

        public async Task<string> UploadFileFromStreamAsync(Stream fileStream, string fileName, string folder)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                Console.WriteLine($"📤 Uploading to Google Drive...");
                Console.WriteLine($"  File: {fileName}");
                Console.WriteLine($"  Folder ID: {_backupFolderId}");

                var fileMetadata = new DriveFile
                {
                    Name = fileName,
                    Parents = !string.IsNullOrEmpty(_backupFolderId)
                        ? new List<string> { _backupFolderId }
                        : null
                };

                var request = _driveService.Files.Create(fileMetadata, fileStream, "application/octet-stream");
                request.Fields = "id, webContentLink, name";

                var result = await request.UploadAsync();

                if (result.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    Console.WriteLine($"❌ Upload status: {result.Status}");
                    Console.WriteLine($"   Exception: {result.Exception?.Message}");
                    throw new Exception($"Failed to upload to Google Drive: {result.Exception?.Message ?? "Unknown error"}");
                }

                var fileId = request.ResponseBody?.Id ?? string.Empty;
                var webLink = request.ResponseBody?.WebContentLink ?? string.Empty;

                Console.WriteLine($"✅ Uploaded to Google Drive: {fileName} (ID: {fileId})");
                Console.WriteLine($"   Web link: {webLink}");

                return fileId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload failed: {ex.Message}");
                throw new Exception($"Failed to upload file to Google Drive: {ex.Message}");
            }
        }

        public async Task DeleteFileAsync(string fileId)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                Console.WriteLine($"🗑️ Deleting Google Drive file: {fileId}");
                var request = _driveService.Files.Delete(fileId);
                await request.ExecuteAsync();
                Console.WriteLine($"✅ Deleted Google Drive file ID: {fileId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete file from Google Drive: {ex.Message}");
            }
        }

        public async Task DeleteFileFromPathAsync(string filePath)
        {
            if (!_isConfigured || _driveService == null || string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine($"⚠️ Cannot delete Google Drive file: Not configured or empty path");
                return;
            }

            try
            {
                Console.WriteLine($"🗑️ Deleting Google Drive file from path: {filePath}");

                if (filePath.Length > 5 && !filePath.Contains("/") && !filePath.Contains(".") && !filePath.Contains("\\"))
                {
                    await DeleteFileAsync(filePath);
                    return;
                }

                var fileName = System.IO.Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    Console.WriteLine($"⚠️ Could not extract filename from: {filePath}");
                    return;
                }

                Console.WriteLine($"🔍 Searching for file: {fileName}");

                var request = _driveService.Files.List();
                request.Q = $"name='{fileName}' and trashed=false";
                if (!string.IsNullOrEmpty(_backupFolderId))
                {
                    request.Q += $" and '{_backupFolderId}' in parents";
                }
                request.Fields = "files(id, name, parents)";
                request.PageSize = 10;

                var result = await request.ExecuteAsync();

                if (result.Files != null && result.Files.Any())
                {
                    foreach (var file in result.Files)
                    {
                        if (file != null && !string.IsNullOrEmpty(file.Id))
                        {
                            Console.WriteLine($"   Found file: {file.Name ?? "unknown"} (ID: {file.Id})");
                            await DeleteFileAsync(file.Id);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ File not found in Google Drive: {fileName}");

                    var fallbackRequest = _driveService.Files.List();
                    fallbackRequest.Q = $"name='{fileName}' and trashed=false";
                    fallbackRequest.Fields = "files(id, name, parents)";
                    var fallbackResult = await fallbackRequest.ExecuteAsync();

                    if (fallbackResult.Files != null && fallbackResult.Files.Any())
                    {
                        foreach (var file in fallbackResult.Files)
                        {
                            if (file != null && !string.IsNullOrEmpty(file.Id))
                            {
                                Console.WriteLine($"   Found file (no folder restriction): {file.Name ?? "unknown"} (ID: {file.Id})");
                                await DeleteFileAsync(file.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete file from Google Drive: {ex.Message}");
            }
        }

        public async Task DownloadFileAsync(string fileId, string destinationPath)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                Console.WriteLine($"📥 Downloading from Google Drive: {fileId}");

                var request = _driveService.Files.Get(fileId);
                using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                var result = await request.DownloadAsync(stream);

                if (result.Status == Google.Apis.Download.DownloadStatus.Completed)
                {
                    Console.WriteLine($"✅ Downloaded Google Drive file: {fileId} to {destinationPath}");
                }
                else
                {
                    throw new Exception($"Download failed with status: {result.Status}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download file from Google Drive: {ex.Message}");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!_isConfigured || _driveService == null)
            {
                Console.WriteLine("❌ Google Drive service not configured");
                return false;
            }

            try
            {
                Console.WriteLine("🔍 Testing Google Drive connection...");

                var request = _driveService.Files.List();
                request.PageSize = 1;
                request.Fields = "files(id, name)";
                var result = await request.ExecuteAsync();

                Console.WriteLine($"✅ Google Drive connection successful!");
                Console.WriteLine($"   Files found: {result.Files?.Count ?? 0}");

                if (!string.IsNullOrEmpty(_backupFolderId))
                {
                    try
                    {
                        var folder = await GetFolderAsync(_backupFolderId);
                        if (folder != null)
                        {
                            Console.WriteLine($"✅ Folder accessible: {folder.Name ?? "unknown"} (ID: {folder.Id ?? "unknown"})");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Folder {_backupFolderId} not found or not accessible");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Cannot access folder: {ex.Message}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Google Drive connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task CleanupOldFilesAsync(string folder, int retentionDays)
        {
            if (!_isConfigured || _driveService == null)
                return;

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                Console.WriteLine($"🧹 Cleaning up Google Drive files older than {retentionDays} days...");

                var query = $"createdTime < '{cutoffDate:yyyy-MM-ddTHH:mm:ss.fffZ}' and trashed=false";
                if (!string.IsNullOrEmpty(_backupFolderId))
                {
                    query += $" and '{_backupFolderId}' in parents";
                }

                var request = _driveService.Files.List();
                request.Q = query;
                request.Fields = "files(id, name, createdTime)";
                request.PageSize = 100;

                var result = await request.ExecuteAsync();
                var deletedCount = 0;

                if (result.Files != null)
                {
                    foreach (var file in result.Files)
                    {
                        if (file != null && !string.IsNullOrEmpty(file.Id))
                        {
                            try
                            {
                                await DeleteFileAsync(file.Id);
                                deletedCount++;

                                // Use GetValueOrDefault to avoid CS8602
                                var createdTime = file.CreatedTimeDateTimeOffset.GetValueOrDefault(DateTimeOffset.MinValue);
                                var fileName = file.Name ?? "unknown";
                                Console.WriteLine($"🗑️ Deleted old file: {fileName} (Created: {createdTime})");
                            }
                            catch (Exception ex)
                            {
                                var fileName = file?.Name ?? "unknown";
                                Console.WriteLine($"❌ Failed to delete {fileName}: {ex.Message}");
                            }
                        }
                    }
                }

                Console.WriteLine($"✅ Cleanup complete: {deletedCount} files deleted");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to cleanup Google Drive files: {ex.Message}");
            }
        }

        // Helper methods
        private async Task<DriveFile?> GetFolderAsync(string folderId)
        {
            try
            {
                var request = _driveService?.Files?.Get(folderId) ?? throw new ArgumentNullException(nameof(folderId));
                request.Fields = "id, name, mimeType";
                var folder = await request.ExecuteAsync();
                return folder;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> CreateFolderAsync(string folderName)
        {
            try
            {
                var folderMetadata = new DriveFile
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder"
                };

                var request = _driveService?.Files?.Create(folderMetadata) ?? throw new ArgumentNullException(nameof(folderMetadata));
                request.Fields = "id";
                var result = await request.ExecuteAsync();

                Console.WriteLine($"✅ Created folder: {folderName} (ID: {result.Id ?? string.Empty})");
                return result.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create folder: {ex.Message}");
                return string.Empty;
            }
        }
    }
}