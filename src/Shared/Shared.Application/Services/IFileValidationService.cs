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

        public static FileValidationOptions MailAttachment = new()
        {
            MaxFileSize = 25 * 1024 * 1024, // 25MB
            AllowAllTypes = true // Allow all file types for mail attachments
        };

        public static FileValidationOptions Document = new()
        {
            MaxFileSize = 10 * 1024 * 1024,
            AllowedMimeTypes = new() { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            AllowedExtensions = new() { ".pdf", ".doc", ".docx" }
        };
    }
}