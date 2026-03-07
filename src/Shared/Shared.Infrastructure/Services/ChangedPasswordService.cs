using System.Security.Cryptography;
using BCrypt.Net;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Domain.Entities;
using DotNetEnv;
using shop_back.src.Shared.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;


public class ChangePasswordService : IChangePasswordService
{
    private readonly IUserRepository _userRepo;
    private readonly IPasswordResetRepository _resetRepo;
    private readonly IMailService _mailService;
    private readonly UserLogHelper _userLogHelper;
    private readonly ILogger<ChangePasswordService> _logger;

    public ChangePasswordService(
        IUserRepository userRepo, 
        IPasswordResetRepository resetRepo, 
        IMailService mailService, 
        UserLogHelper userLogHelper,
        ILogger<ChangePasswordService> logger)
    {
        _userRepo = userRepo;
        _resetRepo = resetRepo;
        _mailService = mailService;
        _userLogHelper = userLogHelper;
        _logger = logger;
    }

    public async Task<ChangePasswordResponseDto> RequestChangePasswordAsync(
        Guid userId, 
        ChangePasswordRequestDto request)
    {
        try
        {
            // 1️⃣ Get user
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // 2️⃣ Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
                throw new Exception("Current password is incorrect");

            // 3️⃣ Check if new password is same as old
            if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.Password))
                throw new Exception("New password must be different from current password");

            // 4️⃣ Mark any existing change tokens as used (security)
            await _resetRepo.MarkExistingTokensAsUsedAsync(userId, "change");

            // 5️⃣ Generate secure token
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("/", "_")
                .Replace("+", "-")
                .TrimEnd('=');

            // 6️⃣ Hash the new password for storage (will be used when verified)
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            // 7️⃣ Create token record
            var changeToken = new PasswordReset
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hour expiry
                Used = false,
                TokenType = "change",
                NewPasswordHash = newPasswordHash,
                CreatedAt = DateTime.UtcNow
            };

            await _resetRepo.AddAsync(changeToken);
            await _resetRepo.SaveChangesAsync();

            // 8️⃣ Send verification email
            await SendChangePasswordEmail(user, token);

            return new ChangePasswordResponseDto
            {
                Message = "Verification email sent successfully",
                RequiresVerification = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RequestChangePasswordAsync for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ValidateChangeTokenAsync(string token)
    {
        var reset = await _resetRepo.GetByTokenAsync(token, "change");
        return reset != null && !reset.Used && reset.ExpiresAt > DateTime.UtcNow;
    }

    public async Task CompleteChangePasswordAsync(VerifyPasswordChangeDto request)
    {
        // 1️⃣ Get and validate token
        var reset = await _resetRepo.GetByTokenAsync(request.Token, "change");
        
        if (reset == null || reset.Used || reset.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Invalid or expired verification token");

        if (string.IsNullOrEmpty(reset.NewPasswordHash))
            throw new Exception("Token data is corrupted");

        // 2️⃣ Get user
        var user = await _userRepo.GetByIdAsync(reset.UserId);
        if (user == null)
            throw new Exception("User not found");

        // 3️⃣ Store old password for logging
        var oldPasswordHash = user.Password;

        // 4️⃣ Update password with the pre-hashed password from token
        user.Password = reset.NewPasswordHash;
        user.UpdatedAt = DateTime.UtcNow;

        // 5️⃣ Mark token as used
        reset.Used = true;

        // 6️⃣ Save changes
        await _userRepo.UpdateAsync(user);
        await _resetRepo.UpdateAsync(reset);
        await _userRepo.SaveChangesAsync();
        await _resetRepo.SaveChangesAsync();

        // 7️⃣ Log the change
        await LogPasswordChange(user, oldPasswordHash);

        // 8️⃣ Send confirmation email
        await SendConfirmationEmail(user);
    }

    private async Task SendChangePasswordEmail(User user, string token)
    {
        // Load environment variables
        var envPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
        try { Env.Load(envPath); } catch { }

        var frontendUrl = Env.GetString("FrontendUrl") ?? "http://localhost:5173";
        var verifyLink = $"{frontendUrl}/verify-password-change/{token}";

        var bodyContent = $@"
            <h2>Password Change Request</h2>
            <p>Hello {user.Name},</p>
            <p>We received a request to change your password.</p>
            <p>Click the button below to confirm this change:</p>
            <p style='text-align:center;'>
                <a href='{verifyLink}' class='button'>Confirm Password Change</a>
            </p>
            <p><strong>This link expires in 1 hour.</strong></p>
            <p>If you did not request this, please ignore this email or contact support immediately.</p>
            <p>For security reasons, your password will not be changed until you confirm.</p>
        ";

        var fullBody = _mailService.BuildEmailTemplate(bodyContent, "Confirm Password Change");

        await _mailService.SendEmailAsync(new Mail
        {
            FromMail = "noreply@shop.com",
            ToMail = user.Email,
            Subject = "Confirm Your Password Change",
            Body = fullBody,
            ModuleName = "Auth",
            Purpose = "PasswordChange",
            CreatedBy = user.Id
        });
    }

    private async Task SendConfirmationEmail(User user)
    {
        var bodyContent = $@"
            <h2>Password Changed Successfully</h2>
            <p>Hello {user.Name},</p>
            <p>Your password has been changed successfully.</p>
            <p>If you didn't make this change, please contact support immediately.</p>
        ";

        var fullBody = _mailService.BuildEmailTemplate(bodyContent, "Password Changed");

        await _mailService.SendEmailAsync(new Mail
        {
            FromMail = "noreply@shop.com",
            ToMail = user.Email,
            Subject = "Your Password Has Been Changed",
            Body = fullBody,
            ModuleName = "Auth",
            Purpose = "PasswordChangeConfirmation",
            CreatedBy = user.Id
        });
    }

    private async Task LogPasswordChange(User user, string oldPasswordHash)
    {
        try
        {
            var changeObject = new
            {
                before = new { Password = "[REDACTED]" }, // Don't log actual passwords
                after = new { Password = "[REDACTED]" },
                action = "Password changed via verification"
            };

            string changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(changeObject);

            await _userLogHelper.LogAsync(
                userId: user.Id,
                actionType: "Update",
                detail: "User changed password via email verification",
                changes: changesJson,
                modelName: "User",
                modelId: user.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log password change for user {UserId}", user.Id);
        }
    }
}