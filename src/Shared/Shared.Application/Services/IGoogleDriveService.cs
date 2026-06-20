// src/Shared/Shared.Application/Services/IGoogleDriveService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Services
{
    public interface IGoogleDriveService
    {
        Task<string> UploadFileAsync(string filePath, string fileName, string folder);
        Task<string> UploadFileFromStreamAsync(Stream fileStream, string fileName, string folder);
        Task DeleteFileAsync(string fileId);
        Task DeleteFileFromPathAsync(string filePath);
        Task DownloadFileAsync(string fileId, string destinationPath);
        Task<bool> TestConnectionAsync();
        Task CleanupOldFilesAsync(string folder, int retentionDays);
    }
}