using System.Security.Cryptography;
using System.Text;

namespace EMS.WebApp.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly string _encryptionKey;
        private const string ENCRYPTION_PREFIX = "ENC:";

        public EncryptionService(IConfiguration configuration)
        {
            // Get encryption key from configuration or use a default one
            _encryptionKey = configuration["EncryptionSettings:Key"] ?? "YourDefaultEncryptionKey2024!@#";

            // Ensure key is 32 bytes for AES-256
            if (_encryptionKey.Length < 32)
            {
                _encryptionKey = _encryptionKey.PadRight(32, '0');
            }
            else if (_encryptionKey.Length > 32)
            {
                _encryptionKey = _encryptionKey.Substring(0, 32);
            }
        }

        public string Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            // If already encrypted, return as is
            if (IsEncrypted(plainText))
                return plainText;

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                    aesAlg.GenerateIV();

                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        // Write IV to the beginning of the stream
                        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }

                        // Add prefix to identify encrypted data
                        return ENCRYPTION_PREFIX + Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error and return the original text
                Console.WriteLine($"Encryption error: {ex.Message}");
                return plainText;
            }
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // If not encrypted, return as is
            if (!IsEncrypted(cipherText))
                return cipherText;

            try
            {
                // Remove the prefix
                string actualCipherText = cipherText.Substring(ENCRYPTION_PREFIX.Length);
                byte[] cipherBytes = Convert.FromBase64String(actualCipherText);

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(_encryptionKey);

                    // Extract IV from the beginning of the cipher bytes
                    byte[] iv = new byte[aesAlg.BlockSize / 8];
                    byte[] actualCipherBytes = new byte[cipherBytes.Length - iv.Length];

                    Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                    Array.Copy(cipherBytes, iv.Length, actualCipherBytes, 0, actualCipherBytes.Length);

                    aesAlg.IV = iv;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(actualCipherBytes))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error and return the original text
                Console.WriteLine($"Decryption error: {ex.Message}");
                return cipherText;
            }
        }

        public bool IsEncrypted(string? text)
        {
            return !string.IsNullOrEmpty(text) && text.StartsWith(ENCRYPTION_PREFIX);
        }
    }
}
