using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Auth
{
    // Request to initiate password change (step 1)
    public class ChangePasswordRequestDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{6,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    // Response after requesting password change
    public class ChangePasswordResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public bool RequiresVerification { get; set; } = true;
    }

    // Request to verify and complete password change (step 2)
    public class VerifyPasswordChangeDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}