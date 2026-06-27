using BLL.DTOs.Jira;
using BLL.Services.Interface;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BLL.Services
{
    /// <summary>
    /// Jira REST API v3 client.
    /// Automatically tries Basic Auth first, then falls back to Bearer token if the
    /// Atlassian org has restricted Basic Auth for SSO/Google accounts
    /// (detected via WWW-Authenticate: OAuth response header).
    /// </summary>
    public class JiraApiService : IJiraApiService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public JiraApiService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // ── HTTP plumbing ────────────────────────────────────────────────────────────

        private static HttpClient CreateClient(string email, string apiToken, bool useBearer)
        {
            email = email.Trim();
            apiToken = apiToken.Trim();

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromSeconds(30)
            };
            var client = new HttpClient(handler, disposeHandler: true);

            if (useBearer)
            {
                // Bearer mode: works even when org-level policy blocks Basic Auth for SSO accounts
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken);
            }
            else
            {
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", b64);
            }

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JGMS/1.0");
            return client;
        }

        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient client, HttpMethod method, string url, HttpContent? content = null)
        {
            const int maxRedirects = 10;
            for (int i = 0; i <= maxRedirects; i++)
            {
                var req = new HttpRequestMessage(method, url);
                if (content != null && (method == HttpMethod.Post || method == HttpMethod.Put))
                    req.Content = content;

                var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if ((resp.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                     resp.StatusCode == System.Net.HttpStatusCode.Found ||
                     resp.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect ||
                     resp.StatusCode == System.Net.HttpStatusCode.PermanentRedirect) &&
                    resp.Headers.Location != null)
                {
                    var loc = resp.Headers.Location;
                    url = loc.IsAbsoluteUri ? loc.AbsoluteUri : new Uri(new Uri(url), loc).AbsoluteUri;
                    if ((resp.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                         resp.StatusCode == System.Net.HttpStatusCode.Found) &&
                        method == HttpMethod.Post)
                    {
                        method = HttpMethod.Get;
                        content = null;
                    }
                    resp.Dispose();
                    continue;
                }

                await resp.Content.LoadIntoBufferAsync();
                return resp;
            }
            throw new Exception($"Too many redirects calling Jira: {url}");
        }

        /// <summary>
        /// Tries Basic Auth. If Atlassian signals OAuth-only via WWW-Authenticate: OAuth,
        /// automatically retries with Bearer token (API tokens work as Bearer on Atlassian Cloud).
        /// </summary>
        private static async Task<HttpResponseMessage> SendWithFallbackAsync(
            HttpMethod method, string url, string email, string apiToken,
            HttpContent? content = null)
        {
            // If communicating through the Atlassian OAuth Cloud ID endpoint, Basic Auth is not supported.
            // We MUST use Bearer Auth immediately.
            if (url.Contains("api.atlassian.com/ex/jira", StringComparison.OrdinalIgnoreCase))
            {
                using var bearerClient = CreateClient(email, apiToken, useBearer: true);
                return await SendAsync(bearerClient, method, url, content);
            }

            using var basicClient = CreateClient(email, apiToken, useBearer: false);
            var resp = await SendAsync(basicClient, method, url, content);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                resp.Headers.TryGetValues("WWW-Authenticate", out var wwwAuth) &&
                wwwAuth.Any(v => v.StartsWith("OAuth", StringComparison.OrdinalIgnoreCase)))
            {
                resp.Dispose();
                using var bearerClient = CreateClient(email, apiToken, useBearer: true);
                return await SendAsync(bearerClient, method, url, content);
            }

            return resp;
        }

        // ── Public Methods ───────────────────────────────────────────────────────────

        private static string NormalizeJiraUrl(string urlOrCloudId)
        {
            if (string.IsNullOrWhiteSpace(urlOrCloudId)) return "";
            
            // If it's a GUID (Cloud ID) from Atlassian OAuth
            if (System.Guid.TryParse(urlOrCloudId, out _))
            {
                return $"https://api.atlassian.com/ex/jira/{urlOrCloudId}";
            }

            if (!urlOrCloudId.StartsWith("http"))
            {
                return $"https://{urlOrCloudId}";
            }

            return urlOrCloudId.TrimEnd('/');
        }


        public async Task<bool> TestConnectionAsync(string jiraUrl, string email, string apiToken)
        {
            try
            {
                var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/myself";
                var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
                if (resp.IsSuccessStatusCode) return true;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<JiraProjectDTO> GetProjectAsync(
            string jiraUrl, string email, string apiToken, string projectKey)
        {
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/project/{projectKey}";
            var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to get Jira project: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");

            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            return new JiraProjectDTO
            {
                Id = root.GetProperty("id").GetString() ?? "",
                Key = root.GetProperty("key").GetString() ?? "",
                Name = root.GetProperty("name").GetString() ?? "",
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                ProjectTypeKey = root.GetProperty("projectTypeKey").GetString() ?? ""
            };
        }

        public async Task<List<JiraIssueDTO>> GetProjectIssuesAsync(
            string jiraUrl, string email, string apiToken, string projectKey)
        {
            var issues = new List<JiraIssueDTO>();
            var seenKeys = new HashSet<string>();
            int startAt = 0;
            const int maxResults = 50;
            bool isLast = false;
            string? nextPageToken = null;

            while (!isLast)
            {
                var jql = Uri.EscapeDataString($"project={projectKey} ORDER BY updated DESC");
                var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/search/jql?jql={jql}&maxResults={maxResults}&fields=*all";
                
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    url += $"&nextPageToken={nextPageToken}";
                }
                else
                {
                    url += $"&startAt={startAt}";
                }
                
                var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
                
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"Failed to get Jira issues: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");

                var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
                
                int addedThisPage = 0;
                if (root.TryGetProperty("issues", out var arr))
                {
                    foreach (var issue in arr.EnumerateArray())
                    {
                        var parsed = ParseJiraIssue(issue);
                        if (seenKeys.Add(parsed.IssueKey))
                        {
                            issues.Add(parsed);
                            addedThisPage++;
                        }
                    }
                }
                
                startAt += maxResults;
                
                if (root.TryGetProperty("nextPageToken", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                {
                    nextPageToken = tokenProp.GetString();
                }

                if (addedThisPage == 0 || (root.TryGetProperty("total", out var totalProp) && issues.Count >= totalProp.GetInt32()))
                {
                    isLast = true;
                }
                else if (arr.GetArrayLength() == 0)
                {
                    isLast = true;
                }
            }

            return issues;
        }

        public async Task<JiraIssueDTO> GetIssueAsync(
            string jiraUrl, string email, string apiToken, string issueKey)
        {
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue/{issueKey}";
            var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to get Jira issue: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
            return ParseJiraIssue(JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement);
        }

        public async Task<JiraIssueDTO> CreateIssueAsync(
            string jiraUrl, string email, string apiToken, CreateJiraIssueDTO dto)
        {
            var fields = new Dictionary<string, object>
            {
                ["project"] = new { key = dto.ProjectKey },
                ["summary"] = dto.Summary,
                ["issuetype"] = new { name = dto.IssueType }
            };
            if (!string.IsNullOrEmpty(dto.Description))
                fields["description"] = BuildAdf(dto.Description);
            if (!string.IsNullOrEmpty(dto.Priority))
                fields["priority"] = new { name = dto.Priority };
            if (!string.IsNullOrEmpty(dto.AssigneeAccountId))
                fields["assignee"] = new { accountId = dto.AssigneeAccountId };
            if (dto.DueDate.HasValue)
                fields["duedate"] = dto.DueDate.Value.ToString("yyyy-MM-dd");

            var body = new StringContent(
                JsonSerializer.Serialize(new { fields }, _jsonOptions), Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue";
            var resp = await SendWithFallbackAsync(HttpMethod.Post, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to create Jira issue: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");

            var issueKey = JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("key").GetString() ?? "";
            return await GetIssueAsync(jiraUrl, email, apiToken, issueKey);
        }

        public async Task<JiraIssueDTO> UpdateIssueAsync(
            string jiraUrl, string email, string apiToken, string issueKey, UpdateJiraIssueDTO dto)
        {
            var fields = new Dictionary<string, object?>();
            if (dto.Summary != null) fields["summary"] = dto.Summary;
            if (dto.Description != null) fields["description"] = BuildAdf(dto.Description);
            if (dto.Priority != null) fields["priority"] = new { name = dto.Priority };
            if (dto.AssigneeAccountId != null) fields["assignee"] = new { accountId = dto.AssigneeAccountId };
            if (dto.DueDate.HasValue) fields["duedate"] = dto.DueDate.Value.ToString("yyyy-MM-dd");

            var body = new StringContent(
                JsonSerializer.Serialize(new { fields }, _jsonOptions), Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue/{issueKey}";
            var resp = await SendWithFallbackAsync(HttpMethod.Put, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to update Jira issue: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");

            return await GetIssueAsync(jiraUrl, email, apiToken, issueKey);
        }

        public async Task<List<JiraTransitionDTO>> GetAvailableTransitionsAsync(
            string jiraUrl, string email, string apiToken, string issueKey)
        {
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue/{issueKey}/transitions";
            var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to get transitions: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");

            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            var transitions = new List<JiraTransitionDTO>();
            if (root.TryGetProperty("transitions", out var arr))
                foreach (var t in arr.EnumerateArray())
                {
                    var to = t.GetProperty("to");
                    transitions.Add(new JiraTransitionDTO
                    {
                        Id = t.GetProperty("id").GetString() ?? "",
                        Name = t.GetProperty("name").GetString() ?? "",
                        To = new JiraStatusDTO
                        {
                            Id = to.GetProperty("id").GetString() ?? "",
                            Name = to.GetProperty("name").GetString() ?? ""
                        }
                    });
                }
            return transitions;
        }

        public async Task TransitionIssueAsync(
            string jiraUrl, string email, string apiToken, string issueKey, string transitionId)
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { transition = new { id = transitionId } }, _jsonOptions),
                Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue/{issueKey}/transitions";
            var resp = await SendWithFallbackAsync(HttpMethod.Post, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to transition issue: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
        }

        public async Task<string?> SearchUserAsync(
            string jiraUrl, string email, string apiToken, string searchTerm)
        {
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/user/search?query={Uri.EscapeDataString(searchTerm)}";
            var resp = await SendWithFallbackAsync(HttpMethod.Get, url, email, apiToken);
            if (!resp.IsSuccessStatusCode) return null;

            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            if (root.GetArrayLength() > 0)
                return root[0].GetProperty("accountId").GetString();
            return null;
        }

        public async Task MoveIssueToSprintAsync(
            string jiraUrl, string email, string apiToken, string issueKey, int sprintId)
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { issues = new[] { issueKey } }, _jsonOptions),
                Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/agile/1.0/sprint/{sprintId}/issue";
            var resp = await SendWithFallbackAsync(HttpMethod.Post, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to move issue to sprint: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
        }

        public async Task MoveIssueToBacklogAsync(
            string jiraUrl, string email, string apiToken, string issueKey)
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { issues = new[] { issueKey } }, _jsonOptions),
                Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/agile/1.0/backlog/issue";
            var resp = await SendWithFallbackAsync(HttpMethod.Post, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to move issue to backlog: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
        }

        public async Task CreateIssueLinkAsync(
            string jiraUrl, string email, string apiToken,
            string fromIssueKey, string toIssueKey, string linkType = "Relates")
        {
            var payload = new
            {
                type = new { name = linkType },
                inwardIssue = new { key = fromIssueKey },
                outwardIssue = new { key = toIssueKey }
            };
            var body = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issueLink";
            var resp = await SendWithFallbackAsync(HttpMethod.Post, url, email, apiToken, body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to create issue link: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
        }

        public async Task DeleteIssueAsync(
            string jiraUrl, string email, string apiToken, string issueKey)
        {
            var url = $"{NormalizeJiraUrl(jiraUrl)}/rest/api/3/issue/{issueKey}";
            var resp = await SendWithFallbackAsync(HttpMethod.Delete, url, email, apiToken);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to delete Jira issue: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static object BuildAdf(string text) => new
        {
            type = "doc",
            version = 1,
            content = new[]
            {
                new { type = "paragraph", content = new[] { new { type = "text", text } } }
            }
        };

        private JiraIssueDTO ParseJiraIssue(JsonElement issue)
        {
            var fields = issue.TryGetProperty("fields", out var f) ? f : default;
            var dto = new JiraIssueDTO
            {
                IssueKey = issue.TryGetProperty("key", out var key) ? key.GetString() ?? "" : "",
                JiraId = issue.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Summary = fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : "",
                IssueType = fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("issuetype", out var it) && it.ValueKind == JsonValueKind.Object && it.TryGetProperty("name", out var itName) ? itName.GetString() ?? "" : "",
                Status = fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Object && st.TryGetProperty("name", out var stName) ? stName.GetString() ?? "" : ""
            };

            if (string.IsNullOrEmpty(dto.IssueKey))
            {
                Console.WriteLine($"\n\n[JIRA PARSE ERROR] Raw JSON from Atlassian: {issue.GetRawText()}\n\n");
                throw new Exception($"Failed to parse Jira issue! Raw JSON: {issue.GetRawText()}");
            }

            if (fields.ValueKind == JsonValueKind.Object)
            {
                if (fields.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null)
                    dto.Description = ExtractTextFromAdf(desc);
                if (fields.TryGetProperty("priority", out var pri) && pri.ValueKind == JsonValueKind.Object && pri.TryGetProperty("name", out var priName))
                    dto.Priority = priName.GetString();
                if (fields.TryGetProperty("assignee", out var asgn) && asgn.ValueKind == JsonValueKind.Object)
                {
                    if (asgn.TryGetProperty("accountId", out var accountId))
                        dto.AssigneeJiraId = accountId.GetString();
                    if (asgn.TryGetProperty("displayName", out var displayName))
                        dto.AssigneeName = displayName.GetString();
                }
                if (fields.TryGetProperty("created", out var created) && created.ValueKind != JsonValueKind.Null)
                    dto.CreatedDate = DateTime.Parse(created.GetString() ?? DateTime.UtcNow.ToString());
                if (fields.TryGetProperty("updated", out var updated) && updated.ValueKind != JsonValueKind.Null)
                    dto.UpdatedDate = DateTime.Parse(updated.GetString() ?? DateTime.UtcNow.ToString());

                if (TryExtractSprint(fields, out var sprintId, out var sprintName, out var sprintState))
                {
                    dto.SprintId = sprintId;
                    dto.SprintName = sprintName;
                    dto.SprintState = sprintState;
                }
            }

            return dto;
        }

        private static bool TryExtractSprint(JsonElement fields, out int? sprintId, out string? sprintName, out string? sprintState)
        {
            sprintId = null;
            sprintName = null;
            sprintState = null;

            foreach (var property in fields.EnumerateObject())
            {
                if (!property.Name.StartsWith("customfield_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = property.Value;

                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        if (TryMapSprintObject(item, out sprintId, out sprintName, out sprintState))
                            return true;
                    }
                }
                else if (value.ValueKind == JsonValueKind.Object)
                {
                    if (TryMapSprintObject(value, out sprintId, out sprintName, out sprintState))
                        return true;
                }
            }

            return false;
        }

        private static bool TryMapSprintObject(JsonElement candidate, out int? sprintId, out string? sprintName, out string? sprintState)
        {
            sprintId = null;
            sprintName = null;
            sprintState = null;

            if (candidate.ValueKind != JsonValueKind.Object)
                return false;

            var hasName = candidate.TryGetProperty("name", out var nameProperty);
            var hasState = candidate.TryGetProperty("state", out var stateProperty);
            var hasId = candidate.TryGetProperty("id", out var idProperty);

            if (!hasId || (!hasName && !hasState))
                return false;

            if (idProperty.ValueKind == JsonValueKind.Number && idProperty.TryGetInt32(out var numericId))
                sprintId = numericId;
            else if (idProperty.ValueKind == JsonValueKind.String && int.TryParse(idProperty.GetString(), out var stringId))
                sprintId = stringId;

            sprintName = hasName ? nameProperty.GetString() : null;
            sprintState = hasState ? stateProperty.GetString() : null;

            return sprintId.HasValue || !string.IsNullOrWhiteSpace(sprintName);
        }

        private static string ExtractTextFromAdf(JsonElement description)
        {
            if (description.ValueKind == JsonValueKind.String)
                return description.GetString() ?? "";

            if (!description.TryGetProperty("content", out var content))
                return "";

            var sb = new StringBuilder();
            foreach (var node in content.EnumerateArray())
            {
                if (!node.TryGetProperty("content", out var inner)) continue;
                foreach (var textNode in inner.EnumerateArray())
                    if (textNode.TryGetProperty("text", out var text))
                        sb.Append(text.GetString());
            }
            return sb.ToString();
        }
    }
}

