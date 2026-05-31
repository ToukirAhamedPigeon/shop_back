using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class FileHelper
    {
        private static RemoteFileHelper? _remoteHelper;
        private static IWebHostEnvironment? _webHostEnvironment;
        
        public static ImageResizeOptions DefaultResizeOptions { get; set; } = new ImageResizeOptions
        {
            Enabled = true,
            MaxWidth = 1000,
            MaxHeight = 1000,
            ResizeMode = ImageResizeMode.Max
        };
        
        public static void Initialize(RemoteFileHelper remoteHelper)
        {
            _remoteHelper = remoteHelper;
        }
        
        public static void Initialize(RemoteFileHelper remoteHelper, IWebHostEnvironment webHostEnvironment)
        {
            _remoteHelper = remoteHelper;
            _webHostEnvironment = webHostEnvironment;
        }

        // Main SaveFileAsync method for mailbox attachments (accepts any file type)
       public static async Task<string> SaveFileAsync(IFormFile file, string folder, bool processImage = true, ImageResizeOptions? resizeOptions = null)
        {
            if (file == null || file.Length == 0)
                return string.Empty;
            
            // Use remote storage if configured
            if (_remoteHelper != null && _remoteHelper.UseRemoteStorage)
            {
                var remotePath = await _remoteHelper.UploadFileAsync(file, folder, processImage, resizeOptions);
                if (!string.IsNullOrEmpty(remotePath))
                    return remotePath;
            }
            
            // Otherwise save locally
            return await SaveFileLocalAsync(file, folder, processImage, resizeOptions);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return Guid.NewGuid().ToString();
            
            // Get filename without path
            fileName = Path.GetFileName(fileName);
            
            // Get extension
            var extension = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            // Replace invalid characters with underscore
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                nameWithoutExt = nameWithoutExt.Replace(c, '_');
            }
            
            // Replace spaces and special characters
            nameWithoutExt = nameWithoutExt
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("__", "_");
            
            // Limit length to 100 characters
            if (nameWithoutExt.Length > 100)
                nameWithoutExt = nameWithoutExt.Substring(0, 100);
            
            // Add timestamp to ensure uniqueness
            var timestamp = DateTime.UtcNow.Ticks;
            
            return $"{timestamp}_{nameWithoutExt}{extension}";
        }

        // Local file save with resizing support
        public static async Task<string> SaveFileLocalAsync(IFormFile file, string folder, bool processImage = true, ImageResizeOptions? resizeOptions = null)
        {
            if (file == null || file.Length == 0)
                return string.Empty;
            
            if (_webHostEnvironment == null)
                throw new InvalidOperationException("FileHelper not initialized. Call Initialize() with IWebHostEnvironment first.");
            
            // Ensure directory exists
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath ?? "wwwroot", "uploads", folder);
            Directory.CreateDirectory(uploadPath);
            
            // Use sanitized original filename
            var fileName = SanitizeFileName(file.FileName);
            var filePath = Path.Combine(uploadPath, fileName);
            
            // Check if it's an image and resize is requested
            var options = resizeOptions ?? DefaultResizeOptions;
            
            if (processImage && options.Enabled && file.ContentType.StartsWith("image/"))
            {
                using var image = await Image.LoadAsync(file.OpenReadStream());
                
                if (image.Width > options.MaxWidth || image.Height > options.MaxHeight)
                {
                    var resizeWidth = options.MaxWidth;
                    var resizeHeight = options.MaxHeight;
                    
                    if (options.ResizeMode == ImageResizeMode.Max)
                    {
                        var ratio = Math.Min((double)options.MaxWidth / image.Width, (double)options.MaxHeight / image.Height);
                        resizeWidth = (int)(image.Width * ratio);
                        resizeHeight = (int)(image.Height * ratio);
                    }
                    
                    image.Mutate(x => x.Resize(resizeWidth, resizeHeight));
                }
                
                var fileExtension = Path.GetExtension(fileName).ToLower();
                if (fileExtension == ".png")
                {
                    await image.SaveAsPngAsync(filePath);
                }
                else if (fileExtension == ".jpg" || fileExtension == ".jpeg")
                {
                    await image.SaveAsJpegAsync(filePath);
                }
                else if (fileExtension == ".webp")
                {
                    await image.SaveAsWebpAsync(filePath);
                }
                else
                {
                    await image.SaveAsPngAsync(filePath);
                }
            }
            else
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }
            
            return $"/uploads/{folder}/{fileName}";
        }

        public static Task<string> SaveFileFromPathAsync(string sourcePath, string folder)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return Task.FromResult(string.Empty);
            
            if (_webHostEnvironment == null)
                throw new InvalidOperationException("FileHelper not initialized. Call Initialize() with IWebHostEnvironment first.");
            
            try
            {
                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath ?? "wwwroot", "uploads", folder);
                Directory.CreateDirectory(uploadPath);
                
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(sourcePath)}";
                var destPath = Path.Combine(uploadPath, fileName);
                
                File.Copy(sourcePath, destPath);
                
                return Task.FromResult($"/uploads/{folder}/{fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file: {ex.Message}");
                return Task.FromResult(string.Empty);
            }
        }

        public static async Task DeleteFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
            
            if (_remoteHelper != null && _remoteHelper.UseRemoteStorage)
            {
                await _remoteHelper.DeleteFileAsync(filePath);
            }
            else
            {
                DeleteFileLocal(filePath);
            }
        }

        public static void DeleteFileLocal(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
            
            if (_webHostEnvironment == null)
                return;
            
            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath ?? "wwwroot", filePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Console.WriteLine($"Deleted local file: {fullPath}");
            }
        }

        // Legacy method for backward compatibility with profile image upload
        // This method has the same name but different parameter order - uses "subFolder" instead of "folder"
        public static async Task<string?> SaveFileForProfileAsync(
            IFormFile file, 
            string subFolder = "users",
            ImageResizeOptions? resizeOptions = null)
        {
            if (file == null || file.Length == 0)
                return null;

            // Validate file for profile images
            if (file.Length > 5 * 1024 * 1024)
                throw new Exception("File size must be less than 5MB");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP images are allowed");

            var result = await SaveFileAsync(file, subFolder, true, resizeOptions);
            return string.IsNullOrEmpty(result) ? null : result;
        }
    }
    
    public class ImageResizeOptions
    {
        public bool Enabled { get; set; } = true;
        public int MaxWidth { get; set; } = 1000;
        public int MaxHeight { get; set; } = 1000;
        public ImageResizeMode ResizeMode { get; set; } = ImageResizeMode.Max;
    }
    
    public enum ImageResizeMode
    {
        Max,
        Stretch,
        Pad
    }
}