using BLL.Services.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Services;

/// <summary>
/// AES-256 token encryption using a fixed key from configuration.
/// Unlike ASP.NET Data Protection, this key never changes on restart.
/// </summary>
public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;

    public TokenEncryptionService(IConfiguration configuration)
    {
        var base64Key = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is not configured in appsettings.");

        _key = Convert.FromBase64String(base64Key);

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:Key must be a 32-byte (256-bit) Base64 string.");
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to cipher text so we can decrypt later
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var fullBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        // Extract the IV from the beginning
        var iv = new byte[aes.BlockSize / 8];
        var cipherBytes = new byte[fullBytes.Length - iv.Length];

        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}

