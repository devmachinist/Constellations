using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Constellations
{
    public partial class Constellation
    {
        private string EncryptionKey { get; set; } = "Default";
        private ConcurrentDictionary<string, string> UserKeys { get; set; } = new ConcurrentDictionary<string, string>();
        public delegate void EncryptionFailedError(string error);
        public event EncryptionFailedError EncryptionError;
        public delegate void DecryptionTimerOutput(string output);
        public event DecryptionTimerOutput DecryptedText;
        /// <summary>
        /// Sets the encryption key for the constellation. In order for constellations to communicate the keys must match.
        /// </summary>
        /// <param name="key">Encryption key used for encrypting and decrypting messages</param>
        /// <returns>The instance of the constellation</returns>
        public Constellation SetKey(string key)
        {
            this.EncryptionKey = key;
            return this;
        }
        /// <summary>
        /// Sets a specific encryption key for a user.
        /// </summary>
        /// <param name="userId">The user ID or unique identifier</param>
        /// <param name="key">The encryption key for the user</param>
        public Constellation SetUserKey(string userId, string key)
        {
            // Add or update the user key in the concurrent dictionary
            UserKeys.AddOrUpdate(userId, key, (id, oldKey) => key);
            return this;
        }
        /// <summary>
        /// Removes a specific encryption key for a user.
        /// </summary>
        /// <param name="userId">The user ID or unique identifier</param>
        public void RemoveUserKey(string userId)
        {
            var retries = 10;
            // Remove the user key in the concurrent dictionary
            for (int i = 0; i < retries; i++)
            {
                if (UserKeys.TryRemove(userId, out var key))
                {
                    break;
                }
                Task.Delay(100);
            }
        }
        /// <summary>
        /// Retrieves the encryption key for the specified user, or falls back to the base encryption key if not found.
        /// It will retry until the dictionary is not being modified.
        /// </summary>
        /// <param name="userId">The user ID or unique identifier</param>
        /// <returns>The encryption key to use</returns>
        private string GetEncryptionKey(string userId = null)
        {
            const int maxRetries = 10; // Maximum number of retries to avoid infinite loops
            int retries = 0;

            if (userId != null)
            {
                while (retries < maxRetries)
                {
                    if (UserKeys.TryGetValue(userId, out var userKey))
                    {
                        return userKey;
                    }

                    retries++;
                    Thread.Sleep(10); // Brief sleep to reduce contention before retrying
                }
            }
            // Fallback to the base encryption key if user-specific key was not found after retries
            return EncryptionKey;
        }
        private static byte[] ConvertTo256BitKey(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // Convert the input string to a byte array and compute the hash
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                // Return the hash, which is 32 bytes long (256 bits)
                return hashBytes;
            }
        }
        /// <summary>
        /// Used to encrypt outgoing json serialized messages.
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public string Encrypt(string plainText, string? userId = null)
        {
            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = ConvertTo256BitKey(GetEncryptionKey(userId));
                    aesAlg.IV = new byte[16]; // Initialization vector (16 bytes for AES)
                    aesAlg.Mode = CipherMode.CBC;

                    var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (var swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                        }

                        var encrypted = msEncrypt.ToArray();
                        return Convert.ToBase64String(encrypted);
                    }
                }
            }
            catch (Exception ex)
            {
                EncryptionError(ex.ToString());
                return "";
            }
        }
        /// <summary>
        /// Used to decrypt incoming streams before serializing them into messages.
        /// It tries all user-specific keys and the base key until successful decryption.
        /// </summary>
        /// <param name="cipherText">The encrypted text as a base64 string</param>
        /// <returns>Decrypted plaintext</returns>
        public string Decrypt(string cipherText, string? id = null)
        {
            // List of all keys (including base encryption key)
            var allKeys = UserKeys.Values.ToList();
            string? enkey = null;
            allKeys.Add(EncryptionKey);
            if (id != null)
            {
                enkey = UserKeys[id];
            }
            if (enkey != null)
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = ConvertTo256BitKey(enkey);
                    aesAlg.IV = new byte[16]; // Same IV used during encryption
                    aesAlg.Mode = CipherMode.CBC;

                    var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                // If decryption succeeds, return the decrypted text

                                var messageString = srDecrypt.ReadToEnd();
                                DecryptedText?.Invoke(messageString + " : Decrypted");
                                return messageString;
                            }
                        }
                    }
                }

            }
            else
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = ConvertTo256BitKey(EncryptionKey);
                    aesAlg.IV = new byte[16]; // Same IV used during encryption
                    aesAlg.Mode = CipherMode.CBC;

                    var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                // If decryption succeeds, return the decrypted text

                                var messageString = srDecrypt.ReadToEnd();
                                DecryptedText?.Invoke(messageString + " : Decrypted");
                                return messageString;
                            }
                        }
                    }
                }
            }
            // If no key succeeds, return an error message or empty string
            EncryptionError?.Invoke("Decryption failed with all available keys.");
            return "";
        }
    }
}
