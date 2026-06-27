using System;

namespace BLL.DTOs.Jira
{
    public class JiraAuthContext
    {
        // Basic Auth fields
        public string? JiraUrl { get; set; }
        public string? JiraEmail { get; set; }
        public string? ApiToken { get; set; }

        // OAuth 2.0 (3LO) fields
        public string? CloudId { get; set; }
        public string? AccessToken { get; set; }

        public bool IsOAuth => !string.IsNullOrEmpty(CloudId) && !string.IsNullOrEmpty(AccessToken);

        public string BaseUrl => IsOAuth 
            ? $"https://api.atlassian.com/ex/jira/{CloudId}" 
            : JiraUrl?.TrimEnd('/') ?? throw new InvalidOperationException("JiraUrl is required for Basic Auth.");

        public static JiraAuthContext FromBasicAuth(string jiraUrl, string email, string apiToken)
        {
            return new JiraAuthContext
            {
                JiraUrl = jiraUrl,
                JiraEmail = email,
                ApiToken = apiToken
            };
        }

        public static JiraAuthContext FromOAuth(string cloudId, string accessToken)
        {
            return new JiraAuthContext
            {
                CloudId = cloudId,
                AccessToken = accessToken
            };
        }
    }
}
