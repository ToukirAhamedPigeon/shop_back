namespace shop_back.src.Shared.Application.DTOs.Auth
{
    // CreatePasswordResetRequestDto.cs
    public class CreatePasswordResetRequestDto
    {
        public string Email { get; set; } = string.Empty;
    }

    // CreatePasswordResetResponseDto.cs
    public class CreatePasswordResetResponseDto
    {
        public string Message { get; set; } = string.Empty;
    }

    // ValidateResetTokenResponseDto.cs
    public class ValidateResetTokenResponseDto
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
        public Guid? UserId { get; set; }
    }

    // ResetPasswordRequestDto.cs
    public class ResetPasswordRequestDto
    {
        public string Token { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
