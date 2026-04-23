using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class FileHelper
    {
        private static RemoteFileHelper? _remoteHelper;
        
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

        public static async Task<string?> SaveFileAsync(
            IFormFile file, 
            string subFolder = "users",
            ImageResizeOptions? resizeOptions = null)
        {
            Console.WriteLine($"=== FileHelper.SaveFileAsync CALLED ===");
            if (_remoteHelper != null)
            {
                Console.WriteLine("✅ Calling RemoteFileHelper.SaveFileAsync");
                return await _remoteHelper.SaveFileAsync(file, subFolder, resizeOptions);
            }
            
            Console.WriteLine("❌ _remoteHelper is NULL, using LOCAL");
            return await SaveFileLocalAsync(file, subFolder, resizeOptions);
        }

        public static async Task DeleteFileAsync(string? filePath)
        {
            if (_remoteHelper != null)
            {
                await _remoteHelper.DeleteFileAsync(filePath);
            }
            else
            {
                DeleteFileLocal(filePath);
            }
        }

        public static async Task<string?> SaveFileLocalAsync(
            IFormFile file, 
            string subFolder = "users",
            ImageResizeOptions? resizeOptions = null)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > 5 * 1024 * 1024)
                throw new Exception("File size must be less than 5MB");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP images are allowed");

            var uploadPath = Path.Combine("wwwroot", "uploads", subFolder);
            Directory.CreateDirectory(uploadPath);

            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var fullPath = Path.Combine(uploadPath, fileName);

            var options = resizeOptions ?? DefaultResizeOptions;
            
            if (options.Enabled && file.ContentType.StartsWith("image/"))
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
                
                var outputExtension = Path.GetExtension(fileName).ToLower();
                if (outputExtension == ".png")
                {
                    await image.SaveAsPngAsync(fullPath);
                }
                else if (outputExtension == ".jpg" || outputExtension == ".jpeg")
                {
                    await image.SaveAsJpegAsync(fullPath);
                }
                else if (outputExtension == ".webp")
                {
                    await image.SaveAsWebpAsync(fullPath);
                }
                else
                {
                    await image.SaveAsPngAsync(fullPath);
                }
            }
            else
            {
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            return $"/uploads/{subFolder}/{fileName}";
        }

        public static void DeleteFileLocal(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var fullPath = Path.Combine("wwwroot", filePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Console.WriteLine($"Deleted local file: {fullPath}");
            }
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