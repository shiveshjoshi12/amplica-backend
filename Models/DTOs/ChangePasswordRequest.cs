namespace BizfreeApp.Models.DTOs
{
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }

    public class RefreshTokenRequest
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }

    /// <summary>
    /// DTO used by admins to forcefully reset another user's password (no current password required).
    /// </summary>
    public class AdminChangePasswordRequest
    {
        public string NewPassword { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }
}
