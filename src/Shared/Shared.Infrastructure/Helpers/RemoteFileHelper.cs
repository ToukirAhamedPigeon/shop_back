using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public class RemoteFileHelper
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string _storageType;
        private readonly string _remoteUrl;
        private readonly string _authToken;

        public RemoteFileHelper(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _storageType = configuration["FILE_STORAGE_TYPE"] ?? "local";
            _remoteUrl = configuration["REMOTE_STORAGE_URL"] ?? "https://shopfiles.pigeonic.com";
            _authToken = configuration["REMOTE_STORAGE_TOKEN"] ?? "";
            
            Console.WriteLine($"RemoteFileHelper initialized with storage type: {_storageType}");
            Console.WriteLine($"Remote URL: {_remoteUrl}");
        }

        public async Task<string?> SaveFileAsync(
            Microsoft.AspNetCore.Http.IFormFile file, 
            string subFolder = "users",
            ImageResizeOptions? resizeOptions = null)
        {
            if (_storageType == "local")
            {
                Console.WriteLine("Using LOCAL storage");
                return await FileHelper.SaveFileLocalAsync(file, subFolder, resizeOptions);
            }

            Console.WriteLine("Using REMOTE storage");
            
            // Remote storage
            if (file == null || file.Length == 0)
                return null;

            // Validate file
            ValidateFile(file);

            // Process image with resizing if needed
            byte[] processedImageBytes;
            string extension;
            
            var options = resizeOptions ?? FileHelper.DefaultResizeOptions;
            
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
                
                extension = Path.GetExtension(file.FileName).ToLower();
                
                using var ms = new MemoryStream();
                if (extension == ".png")
                {
                    await image.SaveAsPngAsync(ms);
                }
                else if (extension == ".jpg" || extension == ".jpeg")
                {
                    await image.SaveAsJpegAsync(ms);
                }
                else if (extension == ".webp")
                {
                    await image.SaveAsWebpAsync(ms);
                }
                else
                {
                    await image.SaveAsPngAsync(ms);
                }
                processedImageBytes = ms.ToArray();
            }
            else
            {
                extension = Path.GetExtension(file.FileName);
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                processedImageBytes = ms.ToArray();
            }

            using var client = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();

            var byteArrayContent = new ByteArrayContent(processedImageBytes);
            byteArrayContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            
            var fileName = $"{Guid.NewGuid()}{extension}";
            content.Add(byteArrayContent, "image", fileName);
            content.Add(new StringContent(subFolder), "folder");
            content.Add(new StringContent(fileName), "fileName");

            client.DefaultRequestHeaders.Add("Authorization", _authToken);

            try
            {
                var response = await client.PostAsync($"{_remoteUrl}/api/upload.php", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Response: {responseContent}");
                
                // Case-insensitive JSON deserialization
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var result = JsonSerializer.Deserialize<RemoteUploadResponse>(responseContent, jsonOptions);
                
                if (result != null && result.Success == true && !string.IsNullOrEmpty(result.Url))
                {
                    Console.WriteLine($"✅ Remote upload successful! URL: {result.Url}");
                    return result.Url;
                }
                
                Console.WriteLine($"Upload failed: {responseContent}");
                throw new Exception($"Upload failed: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remote upload error: {ex.Message}");
                throw; // Don't fallback to local - let the error propagate
            }
        }

        public async Task DeleteFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            if (_storageType == "local")
            {
                FileHelper.DeleteFileLocal(filePath);
                return;
            }

            try
            {
                // Extract folder and filename from URL
                string url = filePath;
                if (!filePath.StartsWith("http"))
                {
                    url = $"{_remoteUrl}{filePath}";
                }
                
                var uri = new Uri(url);
                var segments = uri.Segments;
                
                if (segments.Length >= 3)
                {
                    var folder = segments[segments.Length - 2].TrimEnd('/');
                    var filename = segments.Last();

                    using var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("Authorization", _authToken);

                    var content = new StringContent(
                        JsonSerializer.Serialize(new { folder, fileName = filename }),
                        Encoding.UTF8,
                        "application/json"
                    );

                    await client.PostAsync($"{_remoteUrl}/api/delete.php", content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remote delete failed: {ex.Message}");
            }
        }

        private void ValidateFile(Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file.Length > 5 * 1024 * 1024)
                throw new Exception("File size must be less than 5MB");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new Exception("Only JPG, PNG, WEBP images are allowed");
        }

        private class RemoteUploadResponse
        {
            public bool Success { get; set; }
            public string? Url { get; set; }
            public string? Error { get; set; }
        }
    }
}