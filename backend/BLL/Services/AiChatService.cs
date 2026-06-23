using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using BLL.DTOs.Student;

namespace BLL.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly IChatMessageRepository _chatRepo;

        public AiChatService(HttpClient httpClient, IConfiguration config, IChatMessageRepository chatRepo)
        {
            _httpClient = httpClient;
            _chatRepo = chatRepo;
            // Get base URL (e.g. http://localhost:8080/)
            var baseUrl = config["AiAgentSettings:BaseUrl"] ?? "http://localhost:8080/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(int userId)
        {
            return await _chatRepo.GetByUserIdAsync(userId);
        }

        public async Task<string> SendMessageAsync(int userId, string message)
        {
            // Standard OpenAI format used by most local runners (llama.cpp, LM Studio, etc.)
            var payload = new
            {
                model = "local-model", // Most local servers ignore this field
                messages = new[]
                {
                    new { role = "user", content = message }
                },
                temperature = 0.7,
                stream = false
            };

            string replyContent;

            try
            {
                // Most standard local runners use /v1/chat/completions
                var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", payload);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonString);
                
                var content = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                    
                replyContent = content ?? "No response received.";
            }
            catch (Exception ex)
            {
                replyContent = $"Error connecting to AI Agent at {_httpClient.BaseAddress}v1/chat/completions: {ex.Message}\nMake sure your local model server is running and supports the OpenAI API format.";
            }

            var chatMessage = new ChatMessage
            {
                UserId = userId,
                Message = message,
                Reply = replyContent,
                CreatedAt = DateTime.UtcNow
            };

            await _chatRepo.AddAsync(chatMessage);

            return replyContent;
        }

        public async Task<AiSrsResponseDTO> GenerateSrsContentAsync(string requirementsText)
        {
            var systemPrompt = @"You are a System Analyst. Based on the user's requirements, generate the following sections for an SRS document: Introduction, Scope, ProductPerspective, UserClasses, OperatingEnvironment, AssumptionsDependencies. 
Return ONLY valid JSON matching those exact keys. Do not include markdown code blocks or any other text.";

            var payload = new
            {
                model = "local-model",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = requirementsText }
                },
                temperature = 0.3,
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", payload);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonString);
            
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("Received empty content from AI.");
            }

            // Remove any markdown block syntax if the AI still includes it
            content = content.Trim();
            if (content.StartsWith("```json"))
            {
                content = content.Substring(7);
            }
            if (content.StartsWith("```"))
            {
                content = content.Substring(3);
            }
            if (content.EndsWith("```"))
            {
                content = content.Substring(0, content.Length - 3);
            }

            using var srsDoc = JsonDocument.Parse(content);
            var root = srsDoc.RootElement;

            string? GetStringOrSerialized(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return element.GetString();
                    case JsonValueKind.Array:
                        var list = new List<string>();
                        foreach (var item in element.EnumerateArray())
                        {
                            var str = GetStringOrSerialized(item);
                            if (str != null)
                            {
                                list.Add(str);
                            }
                        }
                        return string.Join("\n", list);
                    case JsonValueKind.Object:
                        return JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return element.GetRawText();
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                    default:
                        return null;
                }
            }

            string? GetPropertyString(string name)
            {
                if (root.TryGetProperty(name, out var prop))
                {
                    return GetStringOrSerialized(prop);
                }
                foreach (var objProp in root.EnumerateObject())
                {
                    if (string.Equals(objProp.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetStringOrSerialized(objProp.Value);
                    }
                }
                return null;
            }

            return new AiSrsResponseDTO
            {
                Introduction = GetPropertyString("Introduction"),
                Scope = GetPropertyString("Scope"),
                ProductPerspective = GetPropertyString("ProductPerspective"),
                UserClasses = GetPropertyString("UserClasses"),
                OperatingEnvironment = GetPropertyString("OperatingEnvironment"),
                AssumptionsDependencies = GetPropertyString("AssumptionsDependencies")
            };
        }
    }
}
