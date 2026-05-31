// src/Shared/Infrastructure/Helpers/RemoteFileHelper.cs
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
        
        public bool UseRemoteStorage => _storageType?.Equals("remote", StringComparison.OrdinalIgnoreCase) == true;

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

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return Guid.NewGuid().ToString();
            
            fileName = Path.GetFileName(fileName);
            var extension = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
                nameWithoutExt = nameWithoutExt.Replace(c, '_');
            
            nameWithoutExt = nameWithoutExt
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("__", "_");
            
            nameWithoutExt = System.Text.RegularExpressions.Regex.Replace(nameWithoutExt, @"[^a-zA-Z0-9_]", "_");
            
            if (nameWithoutExt.Length > 100)
                nameWithoutExt = nameWithoutExt.Substring(0, 100);
            
            var timestamp = DateTime.UtcNow.Ticks;
            return $"{timestamp}_{nameWithoutExt}{extension}";
        }

        public async Task<string?> UploadFileAsync(
            IFormFile file, 
            string folder,
            bool processImage = true,
            ImageResizeOptions? resizeOptions = null)
        {
            if (!UseRemoteStorage || file == null || file.Length == 0)
                return null;

            bool isImage = file.ContentType.StartsWith("image/");
            byte[] fileBytes;
            string fileName = SanitizeFileName(file.FileName);

            if (isImage && processImage && resizeOptions != null && resizeOptions.Enabled)
            {
                using var image = await Image.LoadAsync(file.OpenReadStream());
                
                if (image.Width > resizeOptions.MaxWidth || image.Height > resizeOptions.MaxHeight)
                {
                    var ratio = Math.Min((double)resizeOptions.MaxWidth / image.Width, (double)resizeOptions.MaxHeight / image.Height);
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }
                
                using var ms = new MemoryStream();
                var ext = Path.GetExtension(fileName).ToLower();
                if (ext == ".png")
                    await image.SaveAsPngAsync(ms);
                else if (ext == ".jpg" || ext == ".jpeg")
                    await image.SaveAsJpegAsync(ms);
                else if (ext == ".webp")
                    await image.SaveAsWebpAsync(ms);
                else
                    await image.SaveAsPngAsync(ms);
                    
                fileBytes = ms.ToArray();
            }
            else
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            using var client = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();

            var byteArrayContent = new ByteArrayContent(fileBytes);
            byteArrayContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            
            content.Add(byteArrayContent, "file", fileName);
            content.Add(new StringContent(folder), "folder");
            content.Add(new StringContent(fileName), "fileName");

            if (!string.IsNullOrEmpty(_authToken))
            {
                string tokenValue = _authToken.StartsWith("Bearer ") ? _authToken : $"Bearer {_authToken}";
                client.DefaultRequestHeaders.Add("Authorization", tokenValue);
            }

            try
            {
                var response = await client.PostAsync($"{_remoteUrl}/api/upload.php", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                var result = JsonSerializer.Deserialize<RemoteUploadResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (result != null && result.Success == true && !string.IsNullOrEmpty(result.Url))
                {
                    Console.WriteLine($"✅ Remote upload successful! URL: {result.Url}");
                    return result.Url;
                }
                
                Console.WriteLine($"Upload failed: {responseContent}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remote upload error: {ex.Message}");
                return null;
            }
        }

        public async Task DeleteFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !UseRemoteStorage)
                return;

            try
            {
                using var client = _httpClientFactory.CreateClient();
                
                if (!string.IsNullOrEmpty(_authToken))
                    client.DefaultRequestHeaders.Add("Authorization", _authToken);

                var content = new StringContent(
                    JsonSerializer.Serialize(new { filePath = filePath }),
                    Encoding.UTF8,
                    "application/json"
                );

                await client.PostAsync($"{_remoteUrl}/api/delete.php", content);
                Console.WriteLine($"Remote delete request sent for: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remote delete failed: {ex.Message}");
            }
        }

        private class RemoteUploadResponse
        {
            public bool Success { get; set; }
            public string? Url { get; set; }
            public string? Error { get; set; }
        }
    }
}