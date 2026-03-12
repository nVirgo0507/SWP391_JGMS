namespace BLL.Services.Interface;

public interface ITokenEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);

    /// <summary>
    /// Decrypts using the current key, then re-encrypts using a new key.
    /// Use this during key rotation.
    /// </summary>
    string ReEncrypt(string cipherText, string newBase64Key);
}


