using Microsoft.AspNetCore.Http;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class FileHelper
    {
        public static async Task<string?> SaveFileAsync(IFormFile file, string subFolder = "users")
        {
            if (file == null || file.Length == 0)
                return null;

            // Validate file size (5MB max)
            if (file.Length > 5 * 1024 * 1024)
                throw new Exception("File size must be less than 5MB");

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP images are allowed");

            // Create upload directory if it doesn't exist
            var uploadPath = Path.Combine("wwwroot", "uploads", subFolder);
            Directory.CreateDirectory(uploadPath);

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var fullPath = Path.Combine(uploadPath, fileName);

            // Save file
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path
            return $"/uploads/{subFolder}/{fileName}";
        }

        public static void DeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var fullPath = Path.Combine("wwwroot", filePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}