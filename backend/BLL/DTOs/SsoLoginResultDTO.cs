namespace BLL.DTOs
{
    public class SsoLoginResultDTO
    {
        public bool IsNewUser { get; set; }
        public string? AccessToken { get; set; }
        public AtlassianProfileDTO? Profile { get; set; }
        public string? RefreshToken { get; set; }
        public int? ExpiresIn { get; set; }
    }
}
