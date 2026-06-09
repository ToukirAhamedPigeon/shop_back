// src/Shared/API/Controllers/MailController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Mails;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly IMailService _mailService;

        public MailController(IMailService mailService)
        {
            _mailService = mailService;
        }

        private Guid? GetCurrentUserId()
        {
            var userId = User?.FindFirst("UserId")?.Value 
                         ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(userId, out var parsed) ? parsed : null;
        }

        [HttpGet("statistics")]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> GetStatistics()
        {
            var stats = await _mailService.GetStatisticsAsync();
            return Ok(stats);
        }

        [HttpPost("send")]
        [HasPermissionAny("create-admin-mails")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 200 * 1024 * 1024)]
        public async Task<IActionResult> SendMail([FromForm] SendMailRequest request)
        {
            try
            {
                Console.WriteLine($"=== SEND MAIL REQUEST ===");
                Console.WriteLine($"To: {request.ToMail}");
                Console.WriteLine($"Subject: {request.Subject}");
                Console.WriteLine($"Attachments count: {request.Attachments?.Count ?? 0}");
                
                if (request.Attachments != null)
                {
                    foreach (var file in request.Attachments)
                    {
                        Console.WriteLine($"- {file.FileName}: {file.Length} bytes, {file.ContentType}");
                    }
                }
                
                var userId = GetCurrentUserId();
                var mail = await _mailService.SendEmailAsync(request, userId);
                return Ok(new { success = true, message = "Mail sent successfully", mailId = mail.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMail: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("fetch")]
        [HasPermissionAny("read-admin-mails")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit for fetching (emails with large attachments)
        public async Task<IActionResult> FetchEmails()
        {
            try
            {
                await _mailService.FetchAndStoreEmailsAsync();
                return Ok(new { success = true, message = "Emails fetched successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FetchEmails: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> GetMails([FromBody] MailFilterRequest request)
        {
            var (items, totalCount, grandTotalCount) = await _mailService.GetMailsAsync(request);
            return Ok(new { mails = items, totalCount, grandTotalCount });
        }

        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> GetMail(long id)
        {
            var mail = await _mailService.GetMailByIdAsync(id);
            if (mail == null)
                return NotFound(new { message = "Mail not found" });

            return Ok(mail);
        }

        [HttpPost("{id}/read")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> MarkAsRead(long id)
        {
            await _mailService.MarkAsReadAsync(id);
            return Ok(new { success = true });
        }

        [HttpPost("{id}/unread")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> MarkAsUnread(long id)
        {
            await _mailService.MarkAsUnreadAsync(id);
            return Ok(new { success = true });
        }

        [HttpPost("{id}/star")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> ToggleStar(long id)
        {
            await _mailService.ToggleStarAsync(id);
            return Ok(new { success = true, isStarred = true });
        }

        [HttpPost("{id}/unstar")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> RemoveStar(long id)
        {
            var mail = await _mailService.GetMailByIdAsync(id);
            if (mail != null && mail.IsStarred)
            {
                await _mailService.ToggleStarAsync(id);
            }
            return Ok(new { success = true });
        }

        [HttpPost("{id}/trash")]
        [HasPermissionAny("delete-admin-mails")]
        public async Task<IActionResult> MoveToTrash(long id)
        {
            await _mailService.MoveToTrashAsync(id);
            return Ok(new { success = true });
        }

        [HttpPost("{id}/restore")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> RestoreFromTrash(long id)
        {
            await _mailService.RestoreFromTrashAsync(id);
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-mails")]
        public async Task<IActionResult> DeletePermanently(long id)
        {
            await _mailService.DeletePermanentlyAsync(id);
            return Ok(new { success = true });
        }

        [HttpPost("bulk-action")]
        [HasPermissionAny("update-admin-mails")]
        public async Task<IActionResult> BulkAction([FromBody] BulkMailActionRequest request)
        {
            var result = await _mailService.BulkActionAsync(request);
            return Ok(result);
        }

        [HttpGet("download/{id}")]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> DownloadAttachment(long id, [FromQuery] string fileUrl)
        {
            try
            {
                var decodedUrl = Uri.UnescapeDataString(fileUrl);
                
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(decodedUrl, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                    return NotFound(new { message = "File not found" });
                
                var content = await response.Content.ReadAsByteArrayAsync();
                var fileName = Path.GetFileName(new Uri(decodedUrl).LocalPath);
                
                var fileNameParts = fileName.Split('_');
                if (fileNameParts.Length > 1 && long.TryParse(fileNameParts[0], out _))
                {
                    fileName = string.Join("_", fileNameParts.Skip(1));
                }
                
                return File(content, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DownloadAttachment: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("debug-statistics")]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> DebugStatistics()
        {
            var stats = await _mailService.GetStatisticsAsync();
            Console.WriteLine($"Debug Stats: Sent={stats.TotalSent}, Received={stats.TotalReceived}, Unread={stats.UnreadCount}");
            return Ok(stats);
        }

        [HttpPost("fix-attachment-paths")]
        [HasPermissionAny("read-admin-mails")]
        public async Task<IActionResult> FixAttachmentPaths()
        {
            var fixedCount = 0;
            var notFoundCount = 0;
            var results = new List<object>();
            
            // Get all mails with attachments
            var allMails = await _mailService.GetMailsAsync(new MailFilterRequest { Page = 1, Limit = 1000 });
            
            foreach (var mail in allMails.Items)
            {
                var updated = false;
                var newAttachments = new List<string>();
                
                foreach (var attachment in mail.Attachments)
                {
                    // Extract the filename from the stored URL
                    var fileName = Path.GetFileName(attachment);
                    
                    // Check if the file exists at the expected remote URL
                    var expectedUrl = attachment.StartsWith("http") ? attachment : $"https://shopfiles.pigeonic.com{attachment}";
                    
                    using var httpClient = new HttpClient();
                    var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, expectedUrl));
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        // File doesn't exist at that URL - we need to find the correct one
                        // The correct files have timestamp format: 639158104203873286_filename.jpg
                        Console.WriteLine($"File not found: {expectedUrl}");
                        notFoundCount++;
                        
                        // You might want to manually map or skip
                        newAttachments.Add(attachment); // Keep original for now
                    }
                    else
                    {
                        newAttachments.Add(attachment);
                        if (!attachment.StartsWith("https://"))
                        {
                            updated = true;
                        }
                    }
                }
                
                if (updated)
                {
                    // Update the mail's attachments
                    // You'll need to implement this update in your service
                    fixedCount++;
                    results.Add(new { mailId = mail.Id, oldPaths = mail.Attachments, newPaths = newAttachments });
                }
            }
            
            return Ok(new { fixedCount, notFoundCount, results });
        }
    }
}