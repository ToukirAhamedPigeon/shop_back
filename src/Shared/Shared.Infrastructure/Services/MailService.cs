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
            // Save mail in DB first
            await _mailRepository.AddAsync(mail);
            await _mailRepository.SaveChangesAsync();
            var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
            Console.WriteLine("ENV LOADED FROM: " + envPath);
            try { DotNetEnv.Env.Load(envPath); } catch { }

            // SMTP configuration
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

            await client.SendMailAsync(message);
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
