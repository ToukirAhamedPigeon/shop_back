namespace shop_back.src.Shared.Application.DTOs.Users
{
    public class VerifyPasswordChangeRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}