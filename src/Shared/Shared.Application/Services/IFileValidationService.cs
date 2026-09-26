// src/Shared/Application/Services/IFileValidationService.cs
using Microsoft.AspNetCore.Http;

namespace shop_back.src.Shared.Application.Services
{
    public interface IFileValidationService
    {
        Task ValidateAsync(IFormFile file, FileValidationOptions options);
    }

    public class FileValidationOptions
    {
        public long MaxFileSize { get; set; } = 25 * 1024 * 1024; // 25MB default
        public List<string> AllowedExtensions { get; set; } = new();
        public List<string> AllowedMimeTypes { get; set; } = new();
        public bool AllowAllTypes { get; set; } = false;
    }

    public static class FileValidationPresets
    {
        public static FileValidationOptions ProfileImage = new()
        {
            MaxFileSize = 5 * 1024 * 1024, // 5MB
            AllowedMimeTypes = new() { "image/jpeg", "image/jpg", "image/png", "image/webp" },
            AllowedExtensions = new() { ".jpg", ".jpeg", ".png", ".webp" }
        };

        // Mirrors the allow-list in shop_admin_front ComposeMail.tsx. Checked by
        // extension only: browsers report MIME types inconsistently (empty for
        // .7z/.rar, .csv sent as application/vnd.ms-excel on Windows). HTML and
        // SVG are deliberately excluded because both can carry scripts.
        public static FileValidationOptions MailAttachment = new()
        {
            MaxFileSize = 25 * 1024 * 1024, // 25MB
            AllowedExtensions = new()
            {
                // Images
                ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
                // Documents
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".xml",
                // Archives
                ".zip", ".rar", ".7z",
                // Audio
                ".mp3", ".wav", ".ogg", ".m4a", ".flac",
                // Video
                ".mp4", ".mpeg", ".mpg", ".mov", ".avi", ".mkv"
            }
        };

        public static FileValidationOptions Document = new()
        {
            MaxFileSize = 10 * 1024 * 1024,
            AllowedMimeTypes = new() { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            AllowedExtensions = new() { ".pdf", ".doc", ".docx" }
        };
    }
}