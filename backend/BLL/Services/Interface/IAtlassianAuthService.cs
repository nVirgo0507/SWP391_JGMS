using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IAtlassianAuthService
    {
        /// <summary>
        /// Generates the OAuth 2.0 authorization URL for Atlassian.
        /// </summary>
        string GetAuthorizationUrl(string state);

        /// <summary>
        /// Exchanges the authorization code for an access token and refresh token.
        /// </summary>
        Task<(string AccessToken, string RefreshToken, int ExpiresIn)> ExchangeCodeForTokensAsync(string code);

        /// <summary>
        /// Gets the user's Atlassian Account ID (/me endpoint).
        /// </summary>
        Task<string> GetUserAtlassianAccountIdAsync(string accessToken);

        /// <summary>
        /// Gets the user's full Atlassian Profile (/me endpoint).
        /// </summary>
        Task<BLL.DTOs.AtlassianProfileDTO> GetAtlassianProfileAsync(string accessToken);

        /// <summary>
        /// Gets the accessible Jira Cloud ID (site ID) for the user.
        /// Returns the first available Jira site ID.
        /// </summary>
        Task<string> GetAccessibleJiraCloudIdAsync(string accessToken);

        /// <summary>
        /// Refreshes the OAuth tokens using the refresh token.
        /// </summary>
        Task<(string AccessToken, string RefreshToken, int ExpiresIn)> RefreshTokensAsync(string refreshToken);
    }
}
