using System.Security.Cryptography;
using BCrypt.Net;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Domain.Entities;

public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _userRepo;
    private readonly IPasswordResetRepository _resetRepo;
    private readonly IMailService _mailService;

    public PasswordResetService(IUserRepository userRepo, IPasswordResetRepository resetRepo, IMailService mailService)
    {
        _userRepo = userRepo;
        _resetRepo = resetRepo;
        _mailService = mailService;
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var user = await _userRepo.GetByEmailAsync(email);
        if (user == null) throw new Exception("Email not registered.");

        // Generate token
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); // 64 chars
        var reset = new PasswordReset
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _resetRepo.AddAsync(reset);
        await _resetRepo.SaveChangesAsync();

        // Mail template
        var resetLink = $"https://yourfrontend.com/reset-password/{token}";
        var body = $@"
            <h2>Password Reset Request</h2>
            <p>Hello {user.Name},</p>
            <p>Click the link below to reset your password. This link expires in 1 hour.</p>
            <a href='{resetLink}' target='_blank'>{resetLink}</a>
            <p>If you did not request this, ignore this email.</p>";

        await _mailService.SendEmailAsync(new Mail
        {
            FromMail = "noreply@shop.com",
            ToMail = user.Email,
            Subject = "Reset your password",
            Body = body,
            ModuleName = "Auth",
            Purpose = "PasswordReset",
            CreatedBy = user.Id
        });
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        var reset = await _resetRepo.GetByTokenAsync(token);
        if (reset == null || reset.Used || reset.ExpiresAt < DateTime.UtcNow) return false;
        return true;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var reset = await _resetRepo.GetByTokenAsync(request.Token);
        if (reset == null || reset.Used || reset.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Invalid or expired token.");

        var user = await _userRepo.GetByIdentifierAsync(reset.UserId.ToString());
        if (user == null) throw new Exception("User not found.");

        // Hash password
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);

        reset.Used = true;

        await _userRepo.UpdateAsync(user);
        await _resetRepo.UpdateAsync(reset);
        await _userRepo.SaveChangesAsync();
        await _resetRepo.SaveChangesAsync();
    }
}
