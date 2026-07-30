using System.Security.Cryptography;
using System.Text;

namespace HotelPOS.UI;

/// <summary>
/// Utility สำหรับการ Hash และตรวจสอบรหัสผ่านแบบ PBKDF2 (Rfc2898DeriveBytes)
/// รองรับการอัปเกรดจาก SHA256 และ Plain Text แบบอัตโนมัติ (Backward Compatible)
/// </summary>
public static class PasswordHelper
{
    private const int SaltSize = 16;           // 128-bit salt
    private const int HashSize = 32;           // 256-bit hash
    private const int DefaultIterations = 100_000;

    /// <summary>
    /// Hash รหัสผ่านด้วย PBKDF2 (SHA-256, 100,000 iterations) + Random Salt
    /// รูปแบบผลลัพธ์: "{iterations}:{salt_base64}:{hash_base64}"
    /// </summary>
    public static string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{DefaultIterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// ตรวจสอบรหัสผ่านเทียบกับค่าที่เก็บไว้
    /// รองรับ 3 รูปแบบ: PBKDF2 (ใหม่), SHA256 (กลาง), Plain Text (เดิม)
    /// หากตรวจสอบผ่านด้วย SHA256 หรือ Plain Text จะ auto-upgrade เป็น PBKDF2
    /// </summary>
    /// <returns>(bool isMatch, string? upgradedHash)
    /// — upgradedHash จะมีค่าก็ต่อเมื่อตรวจสอบผ่านแต่ยังไม่ใช่ PBKDF2 (ควรนำไปบันทึก)</returns>
    public static (bool IsMatch, string? UpgradedHash) VerifyPassword(string password, string storedPassword)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedPassword);

        // ---- 1. PBKDF2 Format (iterations:salt:hash) ----
        if (storedPassword.Contains(':'))
        {
            return VerifyPbkdf2(password, storedPassword);
        }

        // ---- 2. SHA256 Format (64 hex chars) ----
        if (storedPassword.Length == 64 && storedPassword.All(c => char.IsAsciiHexDigit(c)))
        {
            string inputHash = ComputeSha256Hex(password);
            if (string.Equals(inputHash, storedPassword, StringComparison.OrdinalIgnoreCase))
            {
                // Auto-upgrade: hash ด้วย PBKDF2 แล้วส่งกลับให้ caller บันทึก
                string newHash = HashPassword(password);
                return (true, newHash);
            }
            return (false, null);
        }

        // ---- 3. Plain Text Format (เดิม, fallback สุดท้าย) ----
        if (string.Equals(password, storedPassword, StringComparison.Ordinal))
        {
            string newHash = HashPassword(password);
            return (true, newHash);
        }

        return (false, null);
    }

    /// <summary>
    /// ตรวจสอบ PBKDF2 hash
    /// </summary>
    private static (bool IsMatch, string? UpgradedHash) VerifyPbkdf2(string password, string storedPassword)
    {
        try
        {
            string[] parts = storedPassword.Split(':');
            if (parts.Length != 3) return (false, null);

            if (!int.TryParse(parts[0], out int iterations) || iterations < 1)
                return (false, null);

            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] storedHash = Convert.FromBase64String(parts[2]);

            byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                storedHash.Length);

            bool isMatch = CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            return (isMatch, null); // ไม่ต้อง upgrade เพราะเป็น PBKDF2 อยู่แล้ว
        }
        catch
        {
            return (false, null);
        }
    }

    /// <summary>
    /// SHA256 Hex (สำหรับการตรวจสอบ backward compatibility เท่านั้น)
    /// </summary>
    private static string ComputeSha256Hex(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
