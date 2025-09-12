namespace shop_back.src.Shared.Application.DTOs.Auth
{
   public class LoginRequestDto
    {
        public string Identifier { get; set; } = string.Empty; // email, username, or mobile
        public string Password { get; set; } = string.Empty;
    }
}
