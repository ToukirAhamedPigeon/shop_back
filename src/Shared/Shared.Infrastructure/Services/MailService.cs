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
                        // Console.WriteLine($"[ATTACHMENT CHECK] => {filePath} | Exists = {File.Exists(filePath)}");

                        if (File.Exists(filePath))
                        {
                            message.Attachments.Add(new Attachment(filePath));
                        }
                        else
                        {
                            Console.WriteLine($"[WARNING] Attachment not found: {filePath}");
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

         public string BuildEmailTemplate(string bodyContent, string subject = "Notification")
        {
            // Load ENV
            var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
            try { Env.Load(envPath); } catch { }

            var companyName = Env.GetString("CompanyName") ?? "My Company";
            var companyAddress = Env.GetString("CompanyAddress") ?? "123, Main Street, City";
            var companyPhone = Env.GetString("CompanyPhone") ?? "+123456789";
            var companyEmail = Env.GetString("CompanyEmail") ?? "info@company.com";

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
                            border-top: 6px solid #4f46e5; /* primary color */
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
    }
}
