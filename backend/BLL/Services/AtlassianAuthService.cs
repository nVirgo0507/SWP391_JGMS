using BLL.Services.Interface;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AtlassianAuthService : IAtlassianAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _scope;

        public AtlassianAuthService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _clientId = config["AtlassianOAuth:ClientId"] ?? throw new ArgumentNullException("Atlassian ClientId missing");
            _clientSecret = config["AtlassianOAuth:ClientSecret"] ?? throw new ArgumentNullException("Atlassian ClientSecret missing");
            _redirectUri = config["AtlassianOAuth:RedirectUri"] ?? throw new ArgumentNullException("Atlassian RedirectUri missing");
            _scope = config["AtlassianOAuth:Scope"] ?? "read:jira-work write:jira-work read:jira-user offline_access";
        }

        public string GetAuthorizationUrl(string state)
        {
            return $"https://auth.atlassian.com/authorize?audience=api.atlassian.com&client_id={_clientId}&scope={Uri.EscapeDataString(_scope)}&redirect_uri={Uri.EscapeDataString(_redirectUri)}&state={Uri.EscapeDataString(state)}&response_type=code&prompt=consent";
        }

        public async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> ExchangeCodeForTokensAsync(string code)
        {
            var requestBody = new
            {
                grant_type = "authorization_code",
                client_id = _clientId,
                client_secret = _clientSecret,
                code = code,
                redirect_uri = _redirectUri
            };

            var response = await _httpClient.PostAsJsonAsync("https://auth.atlassian.com/oauth/token", requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to exchange Atlassian code: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (
                result.GetProperty("access_token").GetString()!,
                result.GetProperty("refresh_token").GetString()!,
                result.GetProperty("expires_in").GetInt32()
            );
        }

        public async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> RefreshTokensAsync(string refreshToken)
        {
            var requestBody = new
            {
                grant_type = "refresh_token",
                client_id = _clientId,
                client_secret = _clientSecret,
                refresh_token = refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("https://auth.atlassian.com/oauth/token", requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to refresh Atlassian token: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (
                result.GetProperty("access_token").GetString()!,
                result.GetProperty("refresh_token").GetString()!,
                result.GetProperty("expires_in").GetInt32()
            );
        }

        public async Task<string> GetUserAtlassianAccountIdAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.atlassian.com/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to get Atlassian user profile");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("account_id").GetString()!;
        }

        public async Task<BLL.DTOs.AtlassianProfileDTO> GetAtlassianProfileAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.atlassian.com/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to get Atlassian user profile");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return new BLL.DTOs.AtlassianProfileDTO
            {
                AccountId = result.GetProperty("account_id").GetString() ?? "",
                Email = result.GetProperty("email").GetString() ?? "",
                Name = result.GetProperty("name").GetString() ?? "",
                Picture = result.GetProperty("picture").GetString() ?? ""
            };
        }

        public async Task<string> GetAccessibleJiraCloudIdAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.atlassian.com/oauth/token/accessible-resources");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to get Atlassian accessible resources");
            }

            var resources = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            // Return the first Jira site available
            foreach (var resource in resources.EnumerateArray())
            {
                if (resource.TryGetProperty("scopes", out var scopes))
                {
                    // Check if it has jira API scopes
                    var scopeStrings = scopes.EnumerateArray().Select(s => s.GetString()).ToList();
                    if (scopeStrings.Any(s => s != null && s.Contains("jira")))
                    {
                        return resource.GetProperty("id").GetString()!;
                    }
                }
            }

            throw new Exception("No Jira resources found for this Atlassian account.");
        }
    }
}
