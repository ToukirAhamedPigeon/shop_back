namespace shop_back.src.Shared.Application.Services
{
    public interface IGoogleDriveService
    {
        Task<string> UploadFileAsync(string filePath, string fileName, string folder);
        Task<string> UploadFileFromStreamAsync(Stream fileStream, string fileName, string folder);
        Task DeleteFileAsync(string fileId);
        Task DownloadFileAsync(string fileId, string destinationPath);
        Task<bool> TestConnectionAsync();
        Task CleanupOldFilesAsync(string folder, int retentionDays);
        Task DeleteFileFromPathAsync(string filePath);
    }
}