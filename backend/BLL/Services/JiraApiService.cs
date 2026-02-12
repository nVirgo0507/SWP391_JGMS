using BLL.DTOs.Jira;
using BLL.Services.Interface;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BLL.Services
{
    /// <summary>
    /// Implementation of Jira REST API v3 client using HttpClient
    /// Documentation: https://developer.atlassian.com/cloud/jira/platform/rest/v3/
    /// </summary>
    public class JiraApiService : IJiraApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonOptions;

        public JiraApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Create HTTP client with Basic Auth headers
        /// </summary>
        private HttpClient CreateAuthenticatedClient(string jiraUrl, string email, string apiToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(jiraUrl.TrimEnd('/'));

            // Basic Authentication: base64(email:apiToken)
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public async Task<bool> TestConnectionAsync(string jiraUrl, string email, string apiToken)
        {
            try
            {
                var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);
                var response = await client.GetAsync("/rest/api/3/myself");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<JiraProjectDTO> GetProjectAsync(string jiraUrl, string email, string apiToken, string projectKey)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);
            var response = await client.GetAsync($"/rest/api/3/project/{projectKey}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get Jira project: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            return new JiraProjectDTO
            {
                Id = root.GetProperty("id").GetString() ?? "",
                Key = root.GetProperty("key").GetString() ?? "",
                Name = root.GetProperty("name").GetString() ?? "",
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                ProjectTypeKey = root.GetProperty("projectTypeKey").GetString() ?? ""
            };
        }

        public async Task<List<JiraIssueDTO>> GetProjectIssuesAsync(string jiraUrl, string email, string apiToken, string projectKey)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);

            // Use JQL (Jira Query Language) to search for issues
            var jql = $"project={projectKey} ORDER BY updated DESC";
            var encodedJql = Uri.EscapeDataString(jql);
            var url = $"/rest/api/3/search?jql={encodedJql}&maxResults=1000&fields=summary,description,status,issuetype,priority,assignee,created,updated";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get Jira issues: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var issues = new List<JiraIssueDTO>();

            if (jsonDoc.RootElement.TryGetProperty("issues", out var issuesArray))
            {
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    issues.Add(ParseJiraIssue(issue));
                }
            }

            return issues;
        }

        public async Task<JiraIssueDTO> GetIssueAsync(string jiraUrl, string email, string apiToken, string issueKey)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);
            var response = await client.GetAsync($"/rest/api/3/issue/{issueKey}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get Jira issue: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            return ParseJiraIssue(jsonDoc.RootElement);
        }

        public async Task<JiraIssueDTO> CreateIssueAsync(string jiraUrl, string email, string apiToken, CreateJiraIssueDTO dto)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);

            var payload = new
            {
                fields = new
                {
                    project = new { key = dto.ProjectKey },
                    summary = dto.Summary,
                    description = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph",
                                content = new[]
                                {
                                    new { type = "text", text = dto.Description ?? "" }
                                }
                            }
                        }
                    },
                    issuetype = new { name = dto.IssueType },
                    priority = dto.Priority != null ? new { name = dto.Priority } : null,
                    assignee = dto.AssigneeAccountId != null ? new { accountId = dto.AssigneeAccountId } : null
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/rest/api/3/issue", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create Jira issue: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            var issueKey = jsonDoc.RootElement.GetProperty("key").GetString() ?? "";

            // Get the created issue to return full details
            return await GetIssueAsync(jiraUrl, email, apiToken, issueKey);
        }

        public async Task<JiraIssueDTO> UpdateIssueAsync(string jiraUrl, string email, string apiToken, string issueKey, UpdateJiraIssueDTO dto)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);

            var fields = new Dictionary<string, object?>();
            if (dto.Summary != null) fields["summary"] = dto.Summary;
            if (dto.Description != null)
            {
                fields["description"] = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[]
                            {
                                new { type = "text", text = dto.Description }
                            }
                        }
                    }
                };
            }
            if (dto.Priority != null) fields["priority"] = new { name = dto.Priority };
            if (dto.AssigneeAccountId != null) fields["assignee"] = new { accountId = dto.AssigneeAccountId };

            var payload = new { fields };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"/rest/api/3/issue/{issueKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update Jira issue: {response.StatusCode} - {error}");
            }

            // Get updated issue
            return await GetIssueAsync(jiraUrl, email, apiToken, issueKey);
        }

        public async Task<List<JiraTransitionDTO>> GetAvailableTransitionsAsync(string jiraUrl, string email, string apiToken, string issueKey)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);
            var response = await client.GetAsync($"/rest/api/3/issue/{issueKey}/transitions");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get transitions: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var transitions = new List<JiraTransitionDTO>();

            if (jsonDoc.RootElement.TryGetProperty("transitions", out var transitionsArray))
            {
                foreach (var transition in transitionsArray.EnumerateArray())
                {
                    var to = transition.GetProperty("to");
                    transitions.Add(new JiraTransitionDTO
                    {
                        Id = transition.GetProperty("id").GetString() ?? "",
                        Name = transition.GetProperty("name").GetString() ?? "",
                        To = new JiraStatusDTO
                        {
                            Id = to.GetProperty("id").GetString() ?? "",
                            Name = to.GetProperty("name").GetString() ?? ""
                        }
                    });
                }
            }

            return transitions;
        }

        public async Task TransitionIssueAsync(string jiraUrl, string email, string apiToken, string issueKey, string transitionId)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);

            var payload = new
            {
                transition = new { id = transitionId }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"/rest/api/3/issue/{issueKey}/transitions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to transition issue: {response.StatusCode} - {error}");
            }
        }

        public async Task<string?> SearchUserAsync(string jiraUrl, string email, string apiToken, string searchTerm)
        {
            var client = CreateAuthenticatedClient(jiraUrl, email, apiToken);
            var encodedQuery = Uri.EscapeDataString(searchTerm);
            var response = await client.GetAsync($"/rest/api/3/user/search?query={encodedQuery}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.GetArrayLength() > 0)
            {
                var firstUser = jsonDoc.RootElement[0];
                return firstUser.GetProperty("accountId").GetString();
            }

            return null;
        }

        /// <summary>
        /// Parse Jira issue JSON to DTO
        /// </summary>
        private JiraIssueDTO ParseJiraIssue(JsonElement issue)
        {
            var fields = issue.GetProperty("fields");
            var dto = new JiraIssueDTO
            {
                IssueKey = issue.GetProperty("key").GetString() ?? "",
                JiraId = issue.GetProperty("id").GetString() ?? "",
                Summary = fields.GetProperty("summary").GetString() ?? "",
                IssueType = fields.GetProperty("issuetype").GetProperty("name").GetString() ?? "",
                Status = fields.GetProperty("status").GetProperty("name").GetString() ?? ""
            };

            // Optional fields
            if (fields.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null)
            {
                dto.Description = ExtractTextFromDescription(desc);
            }

            if (fields.TryGetProperty("priority", out var priority) && priority.ValueKind != JsonValueKind.Null)
            {
                dto.Priority = priority.GetProperty("name").GetString();
            }

            if (fields.TryGetProperty("assignee", out var assignee) && assignee.ValueKind != JsonValueKind.Null)
            {
                dto.AssigneeJiraId = assignee.GetProperty("accountId").GetString();
                dto.AssigneeName = assignee.GetProperty("displayName").GetString();
            }

            if (fields.TryGetProperty("created", out var created))
            {
                dto.CreatedDate = DateTime.Parse(created.GetString() ?? DateTime.UtcNow.ToString());
            }

            if (fields.TryGetProperty("updated", out var updated))
            {
                dto.UpdatedDate = DateTime.Parse(updated.GetString() ?? DateTime.UtcNow.ToString());
            }

            return dto;
        }

        /// <summary>
        /// Extract plain text from Jira's Atlassian Document Format (ADF)
        /// </summary>
        private string ExtractTextFromDescription(JsonElement description)
        {
            if (description.ValueKind == JsonValueKind.String)
            {
                return description.GetString() ?? "";
            }

            // ADF format
            if (description.TryGetProperty("content", out var content))
            {
                var textBuilder = new StringBuilder();
                foreach (var node in content.EnumerateArray())
                {
                    if (node.TryGetProperty("content", out var innerContent))
                    {
                        foreach (var textNode in innerContent.EnumerateArray())
                        {
                            if (textNode.TryGetProperty("text", out var text))
                            {
                                textBuilder.Append(text.GetString());
                            }
                        }
                    }
                }
                return textBuilder.ToString();
            }

            return "";
        }
    }
}

