using System.Security.Cryptography;
using BCrypt.Net;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Domain.Entities;
using DotNetEnv;
using shop_back.src.Shared.Infrastructure.Helpers; 

public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _userRepo;
    private readonly IPasswordResetRepository _resetRepo;
    private readonly IMailService _mailService;
    private readonly UserLogHelper _userLogHelper;

    public PasswordResetService(IUserRepository userRepo, IPasswordResetRepository resetRepo, IMailService mailService, UserLogHelper userLogHelper)
    {
        _userRepo = userRepo;
        _resetRepo = resetRepo;
        _mailService = mailService;
        _userLogHelper = userLogHelper;
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        try
        {
            var user = await _userRepo.GetByEmailAsync(email);
            if (user == null) throw new Exception("Email not registered.");

            // Generate token
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            var reset = new PasswordReset
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Used = false,
                TokenType = "reset", // Set token type
                CreatedAt = DateTime.UtcNow
            };

            await _resetRepo.AddAsync(reset);
            await _resetRepo.SaveChangesAsync();

            // Load environment variables
            var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"));
            try { Env.Load(envPath); } catch { }

            var frontendAdminUrl = Env.GetString("FrontendAdminUrl")!;
            var resetLink = $"{frontendAdminUrl}/reset-password/{token}";

            var bodyContent = $@"
                <h2>Password Reset Request</h2>
                <p>Hello {user.Name},</p>
                <p>Click the button below to reset your password. This link expires in 1 hour.</p>
                <p style='text-align:center;'>
                    <a href='{resetLink}' class='button'>Reset Password</a>
                </p>
                <p>If you did not request this, ignore this email.</p>
            ";
            var fullBody = _mailService.BuildEmailTemplate(bodyContent, "Reset your password");

            await _mailService.SendEmailAsync(new Mail
            {
                FromMail = "noreply@shop.com",
                ToMail = user.Email,
                Subject = "Reset your password",
                Body = fullBody,
                ModuleName = "Auth",
                Purpose = "PasswordReset",
                CreatedBy = user.Id,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RequestPasswordResetAsync: {ex.Message}");
            throw new Exception("Error sending password reset email.", ex);
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        // Specify token type as "reset" for password reset tokens
        var reset = await _resetRepo.GetByTokenAsync(token, "reset");
        if (reset == null || reset.Used || reset.ExpiresAt < DateTime.UtcNow) return false;
        return true;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        // Specify token type as "reset" for password reset tokens
        var reset = await _resetRepo.GetByTokenAsync(request.Token, "reset");
        Console.WriteLine($"[TOKEN CHECK] => {reset?.Token} | Used = {reset?.Used} | ExpiresAt = {reset?.ExpiresAt} | Type = {reset?.TokenType}");

        if (reset == null || reset.Used || reset.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Invalid or expired token.");

        Console.WriteLine($"[USER ID CHECK] => {reset.UserId}");
        var user = await _userRepo.GetByIdAsync(reset.UserId);
        Console.WriteLine($"[USER CHECK] => {user?.Email} | Exists = {user != null}");
        if (user == null) throw new Exception("User not found.");

        // 🟡 Store Old Password Hash
        var oldPasswordHash = user.Password;

        // 🔐 Hash New Password
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);

        reset.Used = true;

        await _userRepo.UpdateAsync(user);
        await _resetRepo.UpdateAsync(reset);
        await _userRepo.SaveChangesAsync();
        await _resetRepo.SaveChangesAsync();

        // ---------------------------------------------------------
        // ✅ CREATE USER LOG ENTRY (Password Reset)
        // ---------------------------------------------------------

        // Convert the changes to JSON string
        var changeObject = new
        {
            before = new { Password = "[REDACTED]" },
            after = new { Password = "[REDACTED]" },
            action = "Password reset via email"
        };

        string changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(changeObject);

        try
        {
            await _userLogHelper.LogAsync(
                userId: user.Id,
                actionType: "Update",
                detail: "User successfully reset password.",
                changes: changesJson,
                modelName: "User",
                modelId: user.Id
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine("UserLog Error (Password Reset): " + ex.Message);
        }
    }
}