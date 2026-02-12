using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using System;
using System.Threading.Tasks;
using DotNetEnv;

namespace shop_back.src.Shared.Application.Services
{
    public class MailVerificationService : IMailVerificationService
    {
        private readonly IMailVerificationRepository _repo;
        private readonly IMailService _mailService;

        // Token validity in hours
        private readonly int _tokenExpiryHours = 24;


        public MailVerificationService(IMailVerificationRepository repo, IMailService mailService)
        {
            _repo = repo;
            _mailService = mailService;
        }

        public async Task SendVerificationEmailAsync(User user)
        {
            var FrontendAdminUrl = DotNetEnv.Env.GetString("FrontendAdminUrl")!;
            // Generate unique token
            var token = Guid.NewGuid().ToString("N");

            var verification = new MailVerification
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(_tokenExpiryHours)
            };

            await _repo.AddAsync(verification);
            await _repo.SaveChangesAsync();

            // Build verification link
            var verificationLink = $"{FrontendAdminUrl}/verify-email?token={token}";

            // Build email body
            var emailBody = $@"
                <p>Hi {user.Name},</p>
                <p>Please verify your email by clicking the link below:</p>
                <a href='{verificationLink}' class='button'>Verify Email</a>
                <p>This link will expire in {_tokenExpiryHours} hours.</p>
            ";

            var mail = new Mail
            {
                FromMail = "no-reply@yourdomain.com",
                ToMail = user.Email,
                Subject = "Email Verification",
                Body = _mailService.BuildEmailTemplate(emailBody, "Verify Your Email")
            };

            await _mailService.SendEmailAsync(mail);
        }

        public async Task<(bool Success, string Message)> VerifyTokenAsync(string token)
        {
            var verification = await _repo.GetByTokenAsync(token);

            if (verification == null)
                return (false, "Token not found.");

            if (verification.IsUsed)
                return (false, "Token has already been used.");

            if (verification.ExpiresAt < DateTime.UtcNow)
                return (false, "Token has expired.");

            // Mark token as used
            verification.IsUsed = true;
            verification.UsedAt = DateTime.UtcNow;

            // Update user's EmailVerifiedAt
            verification.User.EmailVerifiedAt = DateTime.UtcNow;

            await _repo.SaveChangesAsync();

            return (true, "Email verified successfully!");
        }

        public async Task<(bool Success, string Message)> ResendVerificationAsync(Guid userId)
        {
            var existing = await _repo.GetLatestByUserIdAsync(userId);

            if (existing != null && !existing.IsUsed && existing.ExpiresAt > DateTime.UtcNow)
            {
                return (false, "Previous verification email is still valid.");
            }

            var user = existing?.User;

            if (user == null)
                return (false, "User not found.");

            if (user.EmailVerifiedAt != null)
                return (false, "Email already verified.");

            await SendVerificationEmailAsync(user);

            return (true, "Verification email sent successfully.");
        }

    }
}
