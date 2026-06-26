using DAL.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Helpers
{
    public static class TeamLeaderHelper
    {
        public static string SanitizeFileToken(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized.Replace(' ', '_');
        }

        public static PriorityLevel ParsePriorityLevel(string? value) =>
            Enum.TryParse<PriorityLevel>(value?.Trim(), true, out var parsed) ? parsed : PriorityLevel.medium;

        public static string? ToJiraPriority(string? priority) =>
            Enum.TryParse<PriorityLevel>(priority?.Trim(), true, out var parsed) ? ToJiraPriority(parsed) : null;

        public static string? ToJiraPriority(PriorityLevel priority) => priority switch
        {
            PriorityLevel.low => "Low",
            PriorityLevel.medium => "Medium",
            PriorityLevel.high => "High",
            _ => "Medium"
        };

        public static RequirementType ClassifyRequirementType(string? issueType, string? title, string? description)
        {
            var type = issueType?.ToLower() ?? "";

            if (type is "bug" or "improvement" or "enhancement" or "spike" or "technical task" or "sub-task")
                return RequirementType.non_functional;

            var nfrKeywords = new[]
            {
                "performance", "latency", "throughput", "response time", "load time",
                "millisecond", "concurrent", "concurrency",
                "reliability", "availability", "uptime", "downtime", "failover",
                "fault tolerance", "recovery", "backup", "redundancy",
                "security", "authentication", "authorization", "encryption",
                "hashing", "bcrypt", "jwt", "ssl", "tls", "vulnerability",
                "scalability", "maintainability", "usability", "accessibility",
                "compliance", "gdpr", "audit", "logging", "monitoring"
            };

            var searchText = $"{title} {description}".ToLowerInvariant();
            if (nfrKeywords.Any(keyword => searchText.Contains(keyword)))
                return RequirementType.non_functional;

            return RequirementType.functional;
        }

        public static string DecryptToken(string encryptedToken, byte[] encryptionKey)
        {
            if (string.IsNullOrEmpty(encryptedToken)) return string.Empty;

            var fullCipher = Convert.FromBase64String(encryptedToken);
            if (fullCipher.Length < 12 + 16)
                throw new CryptographicException("Invalid encrypted token payload.");

            var nonce = new byte[12];
            var tag = new byte[16];
            var cipherText = new byte[fullCipher.Length - 12 - 16];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, 12);
            Buffer.BlockCopy(fullCipher, 12, tag, 0, 16);
            Buffer.BlockCopy(fullCipher, 28, cipherText, 0, cipherText.Length);

            using var aes = new AesGcm(encryptionKey, 16);
            var plainText = new byte[cipherText.Length];
            aes.Decrypt(nonce, cipherText, tag, plainText);

            return Encoding.UTF8.GetString(plainText);
        }
    }
}
