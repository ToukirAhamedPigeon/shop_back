namespace shop_back.src.Shared.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the response returned after a successful authentication.
    /// Contains access token, optional refresh token, and user info.
    /// </summary>
    public class AuthResponseDto
    {
        /// <summary>
        /// JWT access token
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Refresh token (optional, usually stored in HttpOnly cookie)
        /// </summary>
        public string? RefreshToken { get; set; } = null;

        /// <summary>
        /// Authenticated user info
        /// </summary>
        public UserDto User { get; set; } = null!;
    }

    /// <summary>
    /// Represents a user returned in authentication responses.
    /// </summary>
    public class UserDto
    {
        public Guid Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string MobileNo { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        /// <summary>
        /// Role names assigned to this user
        /// </summary>
        public string[] Roles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Permissions assigned to this user (from roles or direct)
        /// </summary>
        public string[] Permissions { get; set; } = Array.Empty<string>();

        // Optional: expose active status if needed in client
        // public bool IsActive { get; set; }
        // public bool IsDeleted { get; set; }
    }
}
