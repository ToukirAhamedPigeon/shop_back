using System.Net;
using System.Net.Mail;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using DotNetEnv;

namespace shop_back.src.Shared.Application.Services
{
    public class MailService : IMailService
    {
        private readonly IMailRepository _mailRepository;

        public MailService(IMailRepository mailRepository)
        {
            _mailRepository = mailRepository;
        }

        public async Task SendEmailAsync(Mail mail)
        {
            try
            {
                // Save mail in DB
                await _mailRepository.AddAsync(mail);
                await _mailRepository.SaveChangesAsync();

                // Load ENV
                var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
                try { DotNetEnv.Env.Load(envPath); } catch { }

                var smtpHost = DotNetEnv.Env.GetString("SmtpHost")!;
                var smtpPort = DotNetEnv.Env.GetInt("SmtpPort")!;
                var smtpUser = DotNetEnv.Env.GetString("SmtpUser")!;
                var smtpPass = DotNetEnv.Env.GetString("SmtpPass")!;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var message = new MailMessage(mail.FromMail, mail.ToMail)
                {
                    Subject = mail.Subject,
                    Body = mail.Body,
                    IsBodyHtml = true
                };

                // ---- ADD ATTACHMENTS ----
                if (mail.Attachments != null)
                {
                    foreach (var filePath in mail.Attachments)
                    {
                        if (File.Exists(filePath))
                        {
                            message.Attachments.Add(new Attachment(filePath));
                        }
                    }
                }

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                // Log the exception details
                Console.WriteLine($"Error in SendEmailAsync: {ex.Message}");
                throw new Exception("Error sending email.", ex);
            }
        }

        public async Task<Mail?> GetMailByIdAsync(int id)
        {
            return await _mailRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Mail>> GetAllMailsAsync()
        {
            return await _mailRepository.GetAllAsync();
        }
    }
}
