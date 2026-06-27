namespace BLL.DTOs
{
    public class SsoRegisterDTO
    {
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string JiraAccountId { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string StudentCode { get; set; } = null!;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int? ExpiresIn { get; set; }
    }
}
