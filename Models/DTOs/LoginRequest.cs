namespace BizfreeApp.Models.DTOs
{
	public class LoginRequest
	{
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;

        public string? FcmToken { get; set; }
        public string? DeviceId { get; set; }
    }
}
