using Microsoft.AspNetCore.Http;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services;
using Xunit;

namespace Shared.Tests
{
    public class MailAttachmentValidationTests
    {
        private readonly FileValidationService _validator = new();

        private static IFormFile CreateFile(string fileName, string contentType = "application/octet-stream", long length = 16)
        {
            var stream = new MemoryStream(new byte[length]);
            return new FormFile(stream, 0, length, "attachments", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Theory]
        [InlineData("report.pdf", "application/pdf")]
        [InlineData("data.CSV", "application/vnd.ms-excel")] // Windows reports .csv as Excel
        [InlineData("photos.7z", "")]                        // browsers often send no MIME for archives
        [InlineData("Scan.JPEG", "image/jpeg")]
        [InlineData("clip.mkv", "video/x-matroska")]
        public async Task AllowedExtension_IsAccepted(string fileName, string contentType)
        {
            var file = CreateFile(fileName, contentType);

            await _validator.ValidateAsync(file, FileValidationPresets.MailAttachment);
        }

        [Theory]
        [InlineData("evil.html", "text/html")]
        [InlineData("logo.svg", "image/svg+xml")]
        [InlineData("setup.exe", "application/x-msdownload")]
        [InlineData("script.js", "text/javascript")]
        [InlineData("invoice.pdf.exe", "application/pdf")] // double extension, spoofed MIME
        [InlineData("noext", "application/pdf")]
        public async Task DisallowedExtension_IsRejected(string fileName, string contentType)
        {
            var file = CreateFile(fileName, contentType);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _validator.ValidateAsync(file, FileValidationPresets.MailAttachment));
            Assert.StartsWith($"{fileName} is not an allowed file type", ex.Message);
        }

        [Fact]
        public async Task OversizedFile_IsRejected()
        {
            var file = CreateFile("big.pdf", "application/pdf", FileValidationPresets.MailAttachment.MaxFileSize + 1);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _validator.ValidateAsync(file, FileValidationPresets.MailAttachment));
        }

        [Fact]
        public async Task EmptyFile_IsRejected()
        {
            var file = CreateFile("empty.pdf", "application/pdf", 0);

            await Assert.ThrowsAsync<ArgumentException>(
                () => _validator.ValidateAsync(file, FileValidationPresets.MailAttachment));
        }
    }
}
