namespace BLL.DTOs.Admin;

public class RotateKeyDTO
{
    /// <summary>
    /// New AES-256 encryption key encoded as Base64 (must decode to exactly 32 bytes).
    /// Generate with: openssl rand -base64 32
    /// </summary>
    public string NewBase64Key { get; set; } = null!;
}

