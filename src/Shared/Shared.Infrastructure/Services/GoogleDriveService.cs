// src/Shared/Shared.Infrastructure/Services/GoogleDriveService.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Alias to resolve ambiguity
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace shop_back.src.Shared.Application.Services
{
    // public interface IGoogleDriveService
    // {
    //     Task<string> UploadFileAsync(string filePath, string fileName, string folder);
    //     Task<string> UploadFileFromStreamAsync(Stream fileStream, string fileName, string folder);
    //     Task DeleteFileAsync(string fileId);
    //     Task DownloadFileAsync(string fileId, string destinationPath);
    //     Task<bool> TestConnectionAsync();
    //     Task CleanupOldFilesAsync(string folder, int retentionDays);
    //     Task DeleteFileFromPathAsync(string filePath);
    // }

    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly DriveService? _driveService;
        private readonly string? _backupFolderId;
        private readonly bool _isConfigured;

        public GoogleDriveService(IConfiguration configuration)
        {
            var credentialsPath = configuration["GOOGLE_DRIVE_CREDENTIALS_PATH"] ?? "credentials.json";
            var folderId = configuration["GOOGLE_DRIVE_BACKUP_FOLDER_ID"] ?? "";
            
            _backupFolderId = folderId;
            
            try
            {
                if (System.IO.File.Exists(credentialsPath))
                {
#pragma warning disable CS0618
                    using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
                    var credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(DriveService.ScopeConstants.Drive);
#pragma warning restore CS0618

                    _driveService = new DriveService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "ShopSphere Backup",
                    });
                    
                    _isConfigured = true;
                    Console.WriteLine("✅ Google Drive Service configured successfully");
                }
                else
                {
                    Console.WriteLine($"⚠️ Google Drive credentials not found at: {credentialsPath}");
                    _isConfigured = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to initialize Google Drive Service: {ex.Message}");
                _isConfigured = false;
            }
        }

        public async Task<string> UploadFileAsync(string filePath, string fileName, string folder)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return await UploadFileFromStreamAsync(stream, fileName, folder);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload file to Google Drive: {ex.Message}");
            }
        }

        public async Task<string> UploadFileFromStreamAsync(Stream fileStream, string fileName, string folder)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                var fileMetadata = new DriveFile
                {
                    Name = fileName,
                    Parents = new List<string> { _backupFolderId ?? string.Empty }
                };

                // For shared drives, you need to set supportsAllDrives = true
                var request = _driveService.Files.Create(fileMetadata, fileStream, "application/octet-stream");
                request.Fields = "id, webContentLink";
                request.SupportsAllDrives = true; // Important for shared drives

                var result = await request.UploadAsync();
                if (result.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception($"Failed to upload to Google Drive: {result.Exception?.Message}");

                return request.ResponseBody?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload file to Google Drive: {ex.Message}");
            }
        }

        public async Task DeleteFileAsync(string fileId)
        {
            if (!_isConfigured || _driveService == null)
                throw new Exception("Google Drive is not configured");

            try
            {
                await _driveService.Files.Delete(fileId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete file from Google Drive: {ex.Message}");
            }
        }

        public async Task DeleteFileFromPathAsync(string filePath)
        {
            if (!_isConfigured || _driveService == null)
                return;

            try
            {
                var fileName = Path.GetFileName(filePath);
                var request = _driveService.Files.List();
                request.Q = $"name='{fileName}' and '{_backupFolderId}' in parents";
                request.Fields = "files(id, name)";
                
                var result = await request.ExecuteAsync();
                if (result.Files != null && result.Files.Any())
                {
                    foreach (var file in result.Files)
                    {
                        await DeleteFileAsync(file.Id);
                        Console.WriteLine($"🗑️ Deleted Google Drive file: {file.Name}");
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
                var request = _driveService.Files.Get(fileId);
                using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                await request.DownloadAsync(stream);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download file from Google Drive: {ex.Message}");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!_isConfigured || _driveService == null)
                return false;

            try
            {
                var request = _driveService.Files.List();
                request.PageSize = 1;
                request.Fields = "files(id, name)";
                var result = await request.ExecuteAsync();
                return result.Files != null;
            }
            catch
            {
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
                var query = $"'{_backupFolderId}' in parents and createdTime < '{cutoffDate:yyyy-MM-ddTHH:mm:ss.fffZ}'";
                
                var request = _driveService.Files.List();
                request.Q = query;
                request.Fields = "files(id, name, createdTime)";
                
                var result = await request.ExecuteAsync();
                if (result.Files != null)
                {
                    foreach (var file in result.Files)
                    {
                        try
                        {
                            await DeleteFileAsync(file.Id);
                            Console.WriteLine($"🗑️ Deleted old Google Drive file: {file.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Failed to delete Google Drive file {file.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to cleanup Google Drive files: {ex.Message}");
            }
        }
    }
}