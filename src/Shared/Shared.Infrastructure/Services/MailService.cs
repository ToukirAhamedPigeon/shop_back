using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Mails;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using DotNetEnv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Create an alias for your IMailService to avoid ambiguity
using IAppMailService = shop_back.src.Shared.Application.Services.IMailService;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class MailService : IAppMailService
    {
        private readonly IMailRepository _mailRepository;
        private readonly IMailTemplateRepository _templateRepository;
        private readonly IMailAttachmentRepository _attachmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IWebHostEnvironment _environment;

        public MailService(
            IMailRepository mailRepository,
            IMailTemplateRepository templateRepository,
            IMailAttachmentRepository attachmentRepository,
            IUserRepository userRepository,
            IWebHostEnvironment environment)
        {
            _mailRepository = mailRepository;
            _templateRepository = templateRepository;
            _attachmentRepository = attachmentRepository;
            _userRepository = userRepository;
            _environment = environment;
        }

        private string GenerateMessageId()
        {
            var domain = Env.GetString("SmtpHost", "localhost");
            return $"<{Guid.NewGuid()}@{domain}>";
        }

        // Add helper method to get MIME type
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }

        private async Task SendSmtpEmailAsync(Mail mail, List<IFormFile>? attachments = null)
        {
            var smtpHost = Env.GetString("SmtpHost");
            var smtpPort = Env.GetInt("SmtpPort");
            var smtpUser = Env.GetString("SmtpUser");
            var smtpPass = Env.GetString("SmtpPass");

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser))
            {
                Console.WriteLine("SMTP not configured. Email saved to database only.");
                return;
            }

            using var client = new SmtpClient();
            
            if (smtpPort == 587)
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            }
            else if (smtpPort == 465)
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.SslOnConnect);
            }
            else
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto);
            }
            
            await client.AuthenticateAsync(smtpUser, smtpPass);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(mail.FromMail));
            message.To.Add(MailboxAddress.Parse(mail.ToMail));
            message.Subject = mail.Subject;
            message.MessageId = mail.MessageId ?? GenerateMessageId();

            if (!string.IsNullOrEmpty(mail.CcMail))
            {
                foreach (var cc in mail.CcMail.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Cc.Add(MailboxAddress.Parse(cc.Trim()));
                }
            }

            if (!string.IsNullOrEmpty(mail.BccMail))
            {
                foreach (var bcc in mail.BccMail.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
                }
            }

            var bodyBuilder = new BodyBuilder { HtmlBody = mail.Body };

            // Add attachments from the uploaded files (original request)
            if (attachments != null && attachments.Any())
            {
                foreach (var attachment in attachments)
                {
                    if (attachment.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await attachment.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;
                        bodyBuilder.Attachments.Add(attachment.FileName, memoryStream.ToArray());
                    }
                }
            }
            // Fallback: Add attachments from stored paths if no direct attachments
            else if (mail.Attachments != null && mail.Attachments.Any())
            {
                foreach (var attachmentPath in mail.Attachments)
                {
                    if (!string.IsNullOrEmpty(attachmentPath))
                    {
                        // Handle remote URLs
                        if (attachmentPath.StartsWith("http"))
                        {
                            using var httpClient = new HttpClient();
                            var fileBytes = await httpClient.GetByteArrayAsync(attachmentPath);
                            var fileName = Path.GetFileName(new Uri(attachmentPath).LocalPath);
                            // Remove timestamp prefix for display
                            var parts = fileName.Split('_');
                            if (parts.Length > 1 && long.TryParse(parts[0], out _))
                            {
                                fileName = string.Join("_", parts.Skip(1));
                            }
                            bodyBuilder.Attachments.Add(fileName, fileBytes);
                        }
                        // Handle local files
                        else if (File.Exists(attachmentPath))
                        {
                            bodyBuilder.Attachments.Add(attachmentPath);
                        }
                    }
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task<Mail> SendEmailAsync(SendMailRequest request, Guid? userId = null)
        {
            // Debug logging
            Console.WriteLine($"=== MailService.SendEmailAsync ===");
            Console.WriteLine($"To: {request.ToMail}");
            Console.WriteLine($"Attachments in request: {request.Attachments?.Count ?? 0}");
            
            if (request.Attachments != null)
            {
                foreach (var file in request.Attachments)
                {
                    Console.WriteLine($"Processing: {file.FileName}, Size: {file.Length}, Type: {file.ContentType}");
                }
            }
            
            var fromEmail = Env.GetString("CompanyEmail") ?? Env.GetString("SmtpUser");

            // Create mail record first (without attachments)
            var mail = new Mail
            {
                FromMail = fromEmail ?? "system@localhost",
                ToMail = request.ToMail,
                CcMail = request.CcMail,
                BccMail = request.BccMail,
                Subject = request.Subject,
                Body = request.Body,
                ModuleName = request.ModuleName,
                Purpose = request.Purpose,
                Attachments = new List<string>(),
                IsSent = true,
                IsReceived = false,
                IsRead = true,
                MailType = request.MailType ?? "manual",
                ParentMailId = request.ParentMailId,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                MessageId = GenerateMessageId()
            };

            await _mailRepository.AddAsync(mail);
            await _mailRepository.SaveChangesAsync();
            Console.WriteLine($"Mail saved with ID: {mail.Id}");

            // Copy attachments to memory BEFORE the background task
            var attachmentMemoryStreams = new List<(string FileName, byte[] Content, string ContentType)>();
            
            if (request.Attachments != null && request.Attachments.Any())
            {
                foreach (var file in request.Attachments)
                {
                    if (file != null && file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        attachmentMemoryStreams.Add((
                            file.FileName, 
                            memoryStream.ToArray(), 
                            file.ContentType
                        ));
                    }
                }
            }

            // Save attachments to storage (using the original files)
            var attachmentPaths = new List<string>();
            if (request.Attachments != null && request.Attachments.Any())
            {
                Console.WriteLine($"Processing {request.Attachments.Count} attachments...");
                
                foreach (var file in request.Attachments)
                {
                    if (file != null && file.Length > 0)
                    {
                        try
                        {
                            Console.WriteLine($"Saving attachment: {file.FileName}");
                            
                            // Save file to storage with processImage = false
                            var savedPath = await FileHelper.SaveFileAsync(file, "mail_attachments", processImage: false, resizeOptions: null);
                            Console.WriteLine($"Saved to: {savedPath}");
                            
                            if (!string.IsNullOrEmpty(savedPath))
                            {
                                attachmentPaths.Add(savedPath);
                                
                                var attachment = new MailAttachment
                                {
                                    MailId = mail.Id,
                                    FileName = file.FileName,
                                    FilePath = savedPath,
                                    FileSize = file.Length,
                                    MimeType = file.ContentType ?? GetMimeType(file.FileName),
                                    CreatedAt = DateTime.UtcNow
                                };
                                await _attachmentRepository.AddAsync(attachment);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving attachment {file.FileName}: {ex.Message}");
                        }
                    }
                }
                
                await _attachmentRepository.SaveChangesAsync();
                
                if (attachmentPaths.Any())
                {
                    mail.Attachments = attachmentPaths;
                    await _mailRepository.UpdateAsync(mail);
                    await _mailRepository.SaveChangesAsync();
                    Console.WriteLine($"Updated mail with {attachmentPaths.Count} attachment paths");
                }
            }

            // Send actual email via SMTP using the in-memory copies
            _ = Task.Run(async () => {
                try
                {
                    await SendSmtpEmailWithMemoryStreamsAsync(mail, attachmentMemoryStreams);
                    Console.WriteLine("SMTP email sent successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SMTP send failed: {ex.Message}");
                }
            });

            return mail;
        }

        public async Task<MailDetailDto?> GetMailByIdAsync(long id)
        {
            // Remove the !m.IsTrash filter to allow viewing trashed emails
            var mail = await _mailRepository.GetByIdWithRepliesAsync(id);
            if (mail == null) return null;

            // Mark as read if it's a received email (but not if in trash)
            if (mail.IsReceived && !mail.IsRead && !mail.IsTrash)
            {
                await MarkAsReadAsync(id);
                mail.IsRead = true;
            }

            return MapToDetailDto(mail);
        }

        public async Task<(IEnumerable<MailDto> Items, int TotalCount, int GrandTotalCount)> GetMailsAsync(MailFilterRequest request)
        {
            var (items, totalCount, grandTotalCount) = await _mailRepository.GetFilteredAsync(request);
            var dtos = items.Select(MapToDto);
            return (dtos, totalCount, grandTotalCount);
        }

        private string ComputeFileHash(byte[] fileBytes)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(fileBytes);
            return Convert.ToBase64String(hashBytes);
        }
        

        public async Task MarkAsReadAsync(long id)
        {
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail != null && !mail.IsRead)
            {
                mail.IsRead = true;
                mail.UpdatedAt = DateTime.UtcNow;
                await _mailRepository.UpdateAsync(mail);
                await _mailRepository.SaveChangesAsync();
            }
        }

        public async Task MarkAsUnreadAsync(long id)
        {
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail != null && mail.IsRead)
            {
                mail.IsRead = false;
                mail.UpdatedAt = DateTime.UtcNow;
                await _mailRepository.UpdateAsync(mail);
                await _mailRepository.SaveChangesAsync();
            }
        }

        public async Task ToggleStarAsync(long id)
        {
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail != null)
            {
                mail.IsStarred = !mail.IsStarred;
                mail.UpdatedAt = DateTime.UtcNow;
                await _mailRepository.UpdateAsync(mail);
                await _mailRepository.SaveChangesAsync();
            }
        }

        public async Task MoveToTrashAsync(long id)
        {
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail != null && !mail.IsTrash)
            {
                mail.IsTrash = true;
                mail.UpdatedAt = DateTime.UtcNow;
                await _mailRepository.UpdateAsync(mail);
                await _mailRepository.SaveChangesAsync();
            }
        }

        public async Task RestoreFromTrashAsync(long id)
        {
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail != null && mail.IsTrash)
            {
                mail.IsTrash = false;
                mail.UpdatedAt = DateTime.UtcNow;
                await _mailRepository.UpdateAsync(mail);
                await _mailRepository.SaveChangesAsync();
            }
        }

        public async Task DeletePermanentlyAsync(long id)
        {
            // Get mail with attachments
            var mail = await _mailRepository.GetByIdAsync(id);
            if (mail == null)
            {
                Console.WriteLine($"⚠️ Mail with ID {id} not found");
                return;
            }
            
            Console.WriteLine($"🗑️ Permanently deleting mail ID: {id}, Attachments count: {mail.Attachments?.Count ?? 0}");
            
            // Delete physical files from remote storage
            if (mail.Attachments != null && mail.Attachments.Any())
            {
                foreach (var attachmentPath in mail.Attachments)
                {
                    if (!string.IsNullOrEmpty(attachmentPath))
                    {
                        try
                        {
                            // Check if this attachment is used by any other mail
                            var otherMailsWithSameAttachment = await _mailRepository.GetByAttachmentPathAsync(attachmentPath);
                            
                            // Count only other mails (excluding current one)
                            var otherMailsCount = otherMailsWithSameAttachment.Count(m => m.Id != id);
                            
                            Console.WriteLine($"📎 Attachment: {attachmentPath}, Used by {otherMailsCount} other mails");
                            
                            // Only delete if no other mail uses this attachment
                            if (otherMailsCount == 0)
                            {
                                Console.WriteLine($"🗑️ Deleting attachment file: {attachmentPath}");
                                var deleted = await FileHelper.DeleteFileAsync(attachmentPath);
                                if (deleted)
                                {
                                    Console.WriteLine($"✅ Successfully deleted: {attachmentPath}");
                                }
                                else
                                {
                                    Console.WriteLine($"❌ Failed to delete: {attachmentPath}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"📎 Attachment still in use by {otherMailsCount} other mails, skipping delete: {attachmentPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error deleting attachment {attachmentPath}: {ex.Message}");
                        }
                    }
                }
                
                // Delete attachment database records
                await _attachmentRepository.DeleteByMailIdAsync(id);
                Console.WriteLine($"🗑️ Deleted attachment records for mail ID: {id}");
            }
            
            // Delete mail record
            await _mailRepository.DeletePermanentlyAsync(id);
            await _mailRepository.SaveChangesAsync();
            
            Console.WriteLine($"✅ Mail {id} permanently deleted from database");
        }

        public async Task<BulkOperationResponse> BulkActionAsync(BulkMailActionRequest request)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = request.Ids.Count,
                Success = true
            };

            try
            {
                switch (request.Action.ToLower())
                {
                    case "trash":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsTrash = true);
                        break;
                    case "restore":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsTrash = false);
                        break;
                    case "read":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsRead = true);
                        break;
                    case "unread":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsRead = false);
                        break;
                    case "star":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsStarred = true);
                        break;
                    case "unstar":
                        await _mailRepository.BulkUpdateAsync(request.Ids, m => m.IsStarred = false);
                        break;
                    case "delete":
                        foreach (var id in request.Ids)
                        {
                            await DeletePermanentlyAsync(id);
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unknown action: {request.Action}");
                }

                if (request.Action.ToLower() != "delete")
                {
                    await _mailRepository.SaveChangesAsync();
                }
                
                response.SuccessCount = request.Ids.Count;
                response.Message = $"{response.SuccessCount} mail(s) processed successfully";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.FailedCount = request.Ids.Count;
                response.Message = ex.Message;
                response.Errors.Add(new BulkOperationError { Id = 0, Error = ex.Message });
            }

            return response;
        }

        public async Task<MailStatisticsDto> GetStatisticsAsync()
        {
            return await _mailRepository.GetStatisticsAsync();
        }

        // Template Management Methods
        public async Task<MailTemplateDto> CreateTemplateAsync(MailTemplateRequest request, Guid userId)
        {
            if (await _templateRepository.ExistsByNameAsync(request.Name))
                throw new InvalidOperationException($"Template with name '{request.Name}' already exists");

            var template = new MailTemplate
            {
                Name = request.Name,
                Subject = request.Subject,
                Body = request.Body,
                Description = request.Description,
                IsGlobal = request.IsGlobal,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _templateRepository.AddAsync(template);
            await _templateRepository.SaveChangesAsync();

            var result = await MapToTemplateDto(template);
            return result ?? throw new InvalidOperationException("Failed to map template to DTO");
        }

        public async Task<MailTemplateDto> UpdateTemplateAsync(long id, MailTemplateRequest request, Guid userId)
        {
            var template = await _templateRepository.GetByIdAsync(id);
            if (template == null)
                throw new KeyNotFoundException("Template not found");

            if (!template.IsGlobal && template.CreatedBy != userId)
                throw new UnauthorizedAccessException("You don't have permission to edit this template");

            if (await _templateRepository.ExistsByNameAsync(request.Name, id))
                throw new InvalidOperationException($"Template with name '{request.Name}' already exists");

            template.Name = request.Name;
            template.Subject = request.Subject;
            template.Body = request.Body;
            template.Description = request.Description;
            template.IsGlobal = request.IsGlobal;
            template.UpdatedAt = DateTime.UtcNow;

            await _templateRepository.UpdateAsync(template);
            await _templateRepository.SaveChangesAsync();

            var result = await MapToTemplateDto(template);
            return result ?? throw new InvalidOperationException("Failed to map template to DTO");
        }

        public async Task DeleteTemplateAsync(long id, Guid userId)
        {
            var template = await _templateRepository.GetByIdAsync(id);
            if (template == null)
                throw new KeyNotFoundException("Template not found");

            if (!template.IsGlobal && template.CreatedBy != userId)
                throw new UnauthorizedAccessException("You don't have permission to delete this template");

            await _templateRepository.DeleteAsync(template);
            await _templateRepository.SaveChangesAsync();
        }

        public async Task<MailTemplateDto?> GetTemplateAsync(long id)
        {
            var template = await _templateRepository.GetByIdAsync(id);
            if (template == null) return null;
            return await MapToTemplateDto(template);
        }

        public async Task<(IEnumerable<MailTemplateDto> Items, int TotalCount)> GetTemplatesAsync(
            string? q, int page, int limit, bool includeGlobal, Guid? userId)
        {
            var (items, totalCount) = await _templateRepository.GetFilteredAsync(q, page, limit, includeGlobal, userId);
            var dtos = new List<MailTemplateDto>();
            foreach (var item in items)
            {
                var dto = await MapToTemplateDto(item);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }
            return (dtos, totalCount);
        }

        public string BuildEmailTemplate(string bodyContent, string subject = "Notification")
        {
            // Load ENV
            var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
            try { Env.Load(envPath); } catch { }

            var companyName = Env.GetString("COMPANY_NAME") ?? "My Company";
            var companyAddress = Env.GetString("COMPANY_ADDRESS") ?? "123, Main Street, City";
            var companyPhone = Env.GetString("COMPANY_PHONE") ?? "+123456789";
            var companyEmail = Env.GetString("COMPANY_EMAIL") ?? "info@company.com";

            // ================= HTML TEMPLATE =================
            var template = $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>{subject}</title>
                    <style>
                        body {{
                            font-family: 'Arial', sans-serif;
                            background-color: #f5f7fa;
                            color: #333;
                            margin: 0;
                            padding: 0;
                        }}
                        .container {{
                            max-width: 600px;
                            margin: 40px auto;
                            background: #ffffff;
                            border-radius: 10px;
                            overflow: hidden;
                            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
                            border-top: 6px solid #4f46e5;
                        }}
                        .header {{
                            background-color: #4f46e5;
                            color: #fff;
                            padding: 20px;
                            text-align: center;
                            font-size: 28px;
                            font-weight: bold;
                        }}
                        .body {{
                            padding: 30px 20px;
                            font-size: 16px;
                            line-height: 1.6;
                            color: #333;
                        }}
                        .body a {{
                            color: #4f46e5;
                            text-decoration: none;
                        }}
                        .footer {{
                            background-color: #f1f5f9;
                            padding: 20px;
                            text-align: center;
                            font-size: 14px;
                            color: #555;
                        }}
                        .footer a {{
                            color: #4f46e5;
                            text-decoration: none;
                        }}
                        .button {{
                            display: inline-block;
                            padding: 12px 25px;
                            margin: 15px 0;
                            background-color: #4f46e5;
                            color: #fff !important;
                            font-weight: bold;
                            border-radius: 6px;
                            text-decoration: none;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>{companyName}</div>
                        <div class='body'>
                            {bodyContent}
                        </div>
                        <div class='footer'>
                            <p>{companyAddress}</p>
                            <p>Phone: {companyPhone} | Email: <a href='mailto:{companyEmail}'>{companyEmail}</a></p>
                            <p>Best Regards,<br/>{companyName} Team</p>
                        </div>
                    </div>
                </body>
                </html>";

            return template;
        }

    
        public async Task FetchAndStoreEmailsAsync()
        {
            var imapHost = Env.GetString("ImapHost");
            var imapPort = Env.GetInt("ImapPort");
            var imapUser = Env.GetString("SmtpUser");
            var imapPass = Env.GetString("SmtpPass");

            if (string.IsNullOrEmpty(imapHost) || string.IsNullOrEmpty(imapUser))
            {
                Console.WriteLine("IMAP not configured. Cannot fetch emails.");
                return;
            }

            Console.WriteLine($"Connecting to IMAP: {imapHost}:{imapPort}");
            
            using var client = new ImapClient();
            
            try
            {
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
                await client.ConnectAsync(imapHost, imapPort, SecureSocketOptions.SslOnConnect, cancellationToken);
                await client.AuthenticateAsync(imapUser, imapPass, cancellationToken);
                
                Console.WriteLine($"Authenticated as: {imapUser}");

                var inbox = client.Inbox;
                if (inbox == null)
                {
                    Console.WriteLine("Cannot access inbox. Inbox is null.");
                    return;
                }
                
                await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
                Console.WriteLine($"Inbox opened in ReadWrite mode. Total messages: {inbox.Count}");
                Console.WriteLine($"Unread messages: {inbox.Unread}");

                // Get only unread emails from last 7 days
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var query = SearchQuery.And(
                    SearchQuery.Not(SearchQuery.Seen),
                    SearchQuery.SentSince(sevenDaysAgo)
                );
                var uids = await inbox.SearchAsync(query, cancellationToken);
                
                Console.WriteLine($"Found {uids.Count} unread emails from last 7 days");

                int newEmailsCount = 0;

                foreach (var uid in uids)
                {
                    try
                    {
                        var message = await inbox.GetMessageAsync(uid, cancellationToken);
                        var messageId = message.MessageId;
                        
                        Console.WriteLine($"Checking email: {message.Subject} (MessageId: {messageId})");
                        
                        // DUPLICATE CHECK - Check if email already exists
                        bool exists = !string.IsNullOrEmpty(messageId) && await _mailRepository.ExistsByMessageIdAsync(messageId);
                        
                        if (exists)
                        {
                            Console.WriteLine($"⚠️ Email already exists in database: {messageId}");
                            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                            continue;
                        }

                        Console.WriteLine($"✅ Processing new email: {message.Subject} from {message.From}");
                        
                        // Save attachments using FileHelper with duplicate detection
                        var attachmentPaths = new List<string>();
                        var mailAttachments = new List<MailAttachment>();
                        var processedHashes = new HashSet<string>();
                        
                        foreach (var attachment in message.Attachments)
                        {
                            if (attachment is MimePart part && !string.IsNullOrEmpty(part.FileName))
                            {
                                try
                                {
                                    using var memoryStream = new MemoryStream();
                                    if (part.Content != null)
                                    {
                                        await part.Content.DecodeToAsync(memoryStream, cancellationToken);
                                        memoryStream.Position = 0;
                                    }
                                    
                                    var fileBytes = memoryStream.ToArray();
                                    if (fileBytes.Length > 0)
                                    {
                                        var fileName = part.FileName;
                                        var contentType = part.ContentType?.MimeType ?? "application/octet-stream";
                                        
                                        // Compute file hash for duplicate detection
                                        var fileHash = ComputeFileHash(fileBytes);
                                        
                                        // Check if this exact file already exists in database
                                        var existingAttachment = await _attachmentRepository.GetByHashAsync(fileHash);
                                        
                                        if (existingAttachment != null && !string.IsNullOrEmpty(existingAttachment.FilePath))
                                        {
                                            // Reuse existing file path
                                            Console.WriteLine($"📎 Duplicate attachment detected: {fileName}, reusing existing file: {existingAttachment.FilePath}");
                                            attachmentPaths.Add(existingAttachment.FilePath);
                                            
                                            mailAttachments.Add(new MailAttachment
                                            {
                                                FileName = fileName,
                                                FilePath = existingAttachment.FilePath,
                                                FileSize = fileBytes.Length,
                                                MimeType = contentType,
                                                FileHash = fileHash,
                                                CreatedAt = DateTime.UtcNow
                                            });
                                        }
                                        else
                                        {
                                            // Create a fake IFormFile to use FileHelper
                                            var formFile = new FormFile(
                                                new MemoryStream(fileBytes), 
                                                0, 
                                                fileBytes.Length, 
                                                "file", 
                                                fileName)
                                            {
                                                Headers = new HeaderDictionary(),
                                                ContentType = contentType
                                            };
                                            
                                            var savedPath = await FileHelper.SaveFileAsync(formFile, "received_mails", processImage: false, resizeOptions: null);
                                            
                                            if (!string.IsNullOrEmpty(savedPath))
                                            {
                                                Console.WriteLine($"📎 Saved new attachment: {savedPath}");
                                                attachmentPaths.Add(savedPath);
                                                
                                                mailAttachments.Add(new MailAttachment
                                                {
                                                    FileName = fileName,
                                                    FilePath = savedPath,
                                                    FileSize = fileBytes.Length,
                                                    MimeType = contentType,
                                                    FileHash = fileHash,
                                                    CreatedAt = DateTime.UtcNow
                                                });
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error saving attachment {part.FileName}: {ex.Message}");
                                }
                            }
                        }

                        var mail = new Mail
                        {
                            FromMail = message.From?.ToString() ?? "unknown",
                            ToMail = string.Join(",", message.To),
                            CcMail = message.Cc.Any() ? string.Join(",", message.Cc) : null,
                            Subject = message.Subject ?? "No Subject",
                            Body = message.HtmlBody ?? message.TextBody ?? "",
                            Attachments = attachmentPaths,
                            IsSent = false,
                            IsReceived = true,
                            IsRead = false,
                            ReceivedAt = message.Date.DateTime,
                            CreatedAt = message.Date.DateTime,
                            UpdatedAt = DateTime.UtcNow,
                            MessageId = messageId,
                            InReplyTo = message.InReplyTo
                        };

                        await _mailRepository.AddAsync(mail);
                        await _mailRepository.SaveChangesAsync();
                        
                        // Save attachments with the mailId
                        foreach (var attachment in mailAttachments)
                        {
                            attachment.MailId = mail.Id;
                            await _attachmentRepository.AddAsync(attachment);
                        }
                        await _attachmentRepository.SaveChangesAsync();
                        
                        newEmailsCount++;
                        Console.WriteLine($"✅ Email #{newEmailsCount} saved with ID: {mail.Id}");
                        Console.WriteLine($"📎 Total attachments saved/reused: {attachmentPaths.Count}");
                        
                        // Mark as read on server after successful save
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                        Console.WriteLine($"📧 Marked email as read on server");
                    }
                    catch (DbUpdateException dbEx)
                    {
                        if (dbEx.InnerException?.Message?.Contains("duplicate key") == true ||
                            dbEx.Message?.Contains("23505") == true)
                        {
                            Console.WriteLine($"⚠️ Duplicate email detected (race condition), marking as read...");
                            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                        }
                        else
                        {
                            Console.WriteLine($"❌ Database error processing email UID {uid}: {dbEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error processing email UID {uid}: {ex.Message}");
                    }
                }

                Console.WriteLine($"📊 Fetch completed: {newEmailsCount} new emails, {uids.Count - newEmailsCount} duplicates skipped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ IMAP connection/authentication error: {ex.Message}");
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
                Console.WriteLine("IMAP disconnected");
            }
        }

        // Helper method to save attachment from file path
        private async Task<string> SaveAttachmentFromPathAsync(string filePath, string fileName, string contentType)
        {
            // Create a fake IFormFile to use FileHelper
            var fileContent = await File.ReadAllBytesAsync(filePath);
            var stream = new MemoryStream(fileContent);
            
            var formFile = new FormFile(stream, 0, fileContent.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            
            return await FileHelper.SaveFileAsync(formFile, "received_mails", processImage: false, resizeOptions: null);
        }

        // Mapping Methods
        private MailDto MapToDto(Mail mail)
        {
            return new MailDto
            {
                Id = mail.Id,
                FromMail = mail.FromMail,
                ToMail = mail.ToMail,
                CcMail = mail.CcMail,
                BccMail = mail.BccMail,
                Subject = mail.Subject,
                Body = !string.IsNullOrEmpty(mail.Body) && mail.Body.Length > 500 ? mail.Body.Substring(0, 500) + "..." : (mail.Body ?? string.Empty),
                ModuleName = mail.ModuleName,
                Purpose = mail.Purpose,
                IsSent = mail.IsSent,
                IsReceived = mail.IsReceived,
                IsRead = mail.IsRead,
                IsStarred = mail.IsStarred,
                IsTrash = mail.IsTrash,
                SentAt = mail.SentAt,
                ReceivedAt = mail.ReceivedAt,
                MailType = mail.MailType,
                ParentMailId = mail.ParentMailId,
                CreatedByName = mail.CreatedByUser?.Name,
                CreatedAt = mail.CreatedAt,
                UpdatedAt = mail.UpdatedAt
            };
        }

        private MailDetailDto MapToDetailDto(Mail mail)
        {
            return new MailDetailDto
            {
                Id = mail.Id,
                FromMail = mail.FromMail,
                ToMail = mail.ToMail,
                CcMail = mail.CcMail,
                BccMail = mail.BccMail,
                Subject = mail.Subject,
                Body = mail.Body,
                ModuleName = mail.ModuleName,
                Purpose = mail.Purpose,
                Attachments = mail.Attachments,
                IsSent = mail.IsSent,
                IsReceived = mail.IsReceived,
                IsRead = mail.IsRead,
                IsStarred = mail.IsStarred,
                IsTrash = mail.IsTrash,
                SentAt = mail.SentAt,
                ReceivedAt = mail.ReceivedAt,
                MailType = mail.MailType,
                ParentMailId = mail.ParentMailId,
                CreatedByName = mail.CreatedByUser?.Name,
                CreatedAt = mail.CreatedAt,
                UpdatedAt = mail.UpdatedAt,
                InReplyTo = mail.InReplyTo,
                MessageId = mail.MessageId,
                Replies = mail.Replies.Select(MapToDto).ToList()
            };
        }

        private async Task<MailTemplateDto?> MapToTemplateDto(MailTemplate template)
        {
            User? createdByUser = null;
            if (template.CreatedBy.HasValue && template.CreatedBy.Value != Guid.Empty)
            {
                createdByUser = await _userRepository.GetByIdAsync(template.CreatedBy.Value);
            }
            
            var createdByName = createdByUser?.Name;

            return new MailTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Subject = template.Subject,
                Body = template.Body,
                Description = template.Description,
                IsGlobal = template.IsGlobal,
                CreatedByName = createdByName,
                CreatedAt = template.CreatedAt
            };
        }

        private async Task SendSmtpEmailWithMemoryStreamsAsync(Mail mail, List<(string FileName, byte[] Content, string ContentType)> attachments)
        {
            var smtpHost = Env.GetString("SmtpHost");
            var smtpPort = Env.GetInt("SmtpPort");
            var smtpUser = Env.GetString("SmtpUser");
            var smtpPass = Env.GetString("SmtpPass");

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser))
            {
                Console.WriteLine("SMTP not configured. Email saved to database only.");
                return;
            }

            using var client = new SmtpClient();
            
            if (smtpPort == 587)
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            }
            else if (smtpPort == 465)
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.SslOnConnect);
            }
            else
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto);
            }
            
            await client.AuthenticateAsync(smtpUser, smtpPass);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(mail.FromMail));
            message.To.Add(MailboxAddress.Parse(mail.ToMail));
            message.Subject = mail.Subject;
            message.MessageId = mail.MessageId ?? GenerateMessageId();

            if (!string.IsNullOrEmpty(mail.CcMail))
            {
                foreach (var cc in mail.CcMail.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Cc.Add(MailboxAddress.Parse(cc.Trim()));
                }
            }

            if (!string.IsNullOrEmpty(mail.BccMail))
            {
                foreach (var bcc in mail.BccMail.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
                }
            }

            var bodyBuilder = new BodyBuilder { HtmlBody = mail.Body };

            // Add attachments from in-memory copies
            foreach (var attachment in attachments)
            {
                bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content);
            }

            message.Body = bodyBuilder.ToMessageBody();

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}