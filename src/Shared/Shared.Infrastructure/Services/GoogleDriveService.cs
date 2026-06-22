// src/Shared/Shared.Infrastructure/Services/GoogleDriveService.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
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
        private readonly DriveService? _driveService;
        private string? _backupFolderId;
        private readonly bool _isConfigured;
        private readonly IHostEnvironment _environment;

        public GoogleDriveService(IConfiguration configuration, IHostEnvironment environment)
        {
            _environment = environment;
            
            // Try multiple possible locations for credentials.json
            var credentialsPath = configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"] ?? "credentials.json";
            
            // Check if the path is relative, if so look in multiple locations
            if (!Path.IsPathRooted(credentialsPath))
            {
                var possiblePaths = new List<string>
                {
                    // Current directory (usually the API project)
                    Path.Combine(Directory.GetCurrentDirectory(), credentialsPath),
                    // Shared.API folder
                    Path.Combine(Directory.GetCurrentDirectory(), "src", "Shared", "Shared.API", credentialsPath),
                    // Content root
                    Path.Combine(_environment.ContentRootPath, credentialsPath),
                    // Web root
                    Path.Combine(_environment.ContentRootPath, "wwwroot", credentialsPath),
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
            Console.WriteLine($"  Credentials Path: {credentialsPath}");
            Console.WriteLine($"  Folder ID: {_backupFolderId}");
            Console.WriteLine($"  File exists: {System.IO.File.Exists(credentialsPath)}");
            
            try
            {
                if (System.IO.File.Exists(credentialsPath))
                {
                    Console.WriteLine($"📄 Reading credentials from: {credentialsPath}");
                    #pragma warning disable CS0618 // Disable obsolete warning for GoogleCredential.FromStream
                    // Use the recommended approach with CredentialFactory
                    using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
                    
                    // Load credentials using the recommended method (synchronous)
                    // This avoids the async constructor issue
                    var credential = GoogleCredential.FromStream(stream);
                    
                    // Check if it's a service account or OAuth2
                    if (credential.UnderlyingCredential is ServiceAccountCredential)
                    {
                        Console.WriteLine("🔐 Using Service Account credentials");
                        // Service accounts need Drive scope
                        credential = credential.CreateScoped(DriveService.ScopeConstants.Drive);
                    }
                    else
                    {
                        Console.WriteLine("🔐 Using OAuth2 credentials");
                        credential = credential.CreateScoped(DriveService.ScopeConstants.Drive);
                    }

                    _driveService = new DriveService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "ShopSphere Backup",
                    });
                    
                    _isConfigured = true;
                    Console.WriteLine("✅ Google Drive Service configured successfully");
                    
                    // Test the connection in background (don't await in constructor)
                    _ = Task.Run(async () => {
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
                    Console.WriteLine($"❌ Google Drive credentials not found at: {credentialsPath}");
                    Console.WriteLine("   Please ensure credentials.json is in the Shared.API folder");
                    _isConfigured = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to initialize Google Drive Service: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                _isConfigured = false;
            }
        }

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

                // Check if folder exists and is accessible
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
                // Don't throw, just log - the backup record will still be soft-deleted
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
                
                // If it's a file ID (no slashes, no dots, longer than 5 chars)
                if (filePath.Length > 5 && !filePath.Contains("/") && !filePath.Contains(".") && !filePath.Contains("\\"))
                {
                    await DeleteFileAsync(filePath);
                    return;
                }
                
                // Extract filename from path
                var fileName = System.IO.Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    Console.WriteLine($"⚠️ Could not extract filename from: {filePath}");
                    return;
                }
                
                Console.WriteLine($"🔍 Searching for file: {fileName}");
                
                // Search for the file
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
                            Console.WriteLine($"   Found file: {file.Name} (ID: {file.Id})");
                            await DeleteFileAsync(file.Id);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ File not found in Google Drive: {fileName}");
                    
                    // Try search without folder restriction
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
                                Console.WriteLine($"   Found file (no folder restriction): {file.Name} (ID: {file.Id})");
                                await DeleteFileAsync(file.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete file from Google Drive: {ex.Message}");
                // Don't throw, just log
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
                
                // Try to list files
                var request = _driveService.Files.List();
                request.PageSize = 1;
                request.Fields = "files(id, name)";
                var result = await request.ExecuteAsync();
                
                Console.WriteLine($"✅ Google Drive connection successful!");
                Console.WriteLine($"   Files found: {result.Files?.Count ?? 0}");
                
                // Test folder access if folder ID is set
                if (!string.IsNullOrEmpty(_backupFolderId))
                {
                    try
                    {
                        var folder = await GetFolderAsync(_backupFolderId);
                        if (folder != null)
                        {
                            Console.WriteLine($"✅ Folder accessible: {folder.Name} (ID: {folder.Id})");
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
                                // Use CreatedTimeDateTimeOffset if available, otherwise use CreatedTime
                                var createdTime = file.CreatedTimeDateTimeOffset ?? file.CreatedTime;
                                Console.WriteLine($"🗑️ Deleted old file: {file.Name} (Created: {createdTime})");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Failed to delete {file.Name}: {ex.Message}");
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
                #pragma warning disable CS8602 // Disable obsolete warning for GoogleCredential.FromStream
                var request = _driveService.Files.Get(folderId);
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
                #pragma warning disable CS8602 // Disable obsolete warning for GoogleCredential.FromStream
                var request = _driveService.Files.Create(folderMetadata);
                request.Fields = "id";
                var result = await request.ExecuteAsync();
                
                Console.WriteLine($"✅ Created folder: {folderName} (ID: {result.Id})");
                return result.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create folder: {ex.Message}");
                return string.Empty;
            }
        }
    }
}