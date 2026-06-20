using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Collections.Generic;

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
    }
}
