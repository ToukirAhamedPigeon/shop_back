// src/Shared/Infrastructure/Services/FileValidationService.cs
using Microsoft.AspNetCore.Http;
using shop_back.src.Shared.Application.Services;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class FileValidationService : IFileValidationService
    {
        public Task ValidateAsync(IFormFile file, FileValidationOptions options)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            // Check file size
            if (file.Length > options.MaxFileSize)
                throw new InvalidOperationException($"File size exceeds {options.MaxFileSize / (1024 * 1024)}MB limit");

            // If all types are allowed, skip further validation
            if (options.AllowAllTypes)
                return Task.CompletedTask;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = file.ContentType.ToLowerInvariant();

            // Check extension
            if (options.AllowedExtensions.Any() && !options.AllowedExtensions.Contains(extension))
                throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", options.AllowedExtensions)}");

            // Check MIME type
            if (options.AllowedMimeTypes.Any() && !options.AllowedMimeTypes.Contains(contentType))
                throw new InvalidOperationException($"File type '{contentType}' is not allowed");

            return Task.CompletedTask;
        }
    }
}