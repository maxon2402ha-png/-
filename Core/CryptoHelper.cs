using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace КР_Ханников.Core
{
                [SupportedOSPlatform("windows")]
    public static class CryptoHelper
    {
                                public static string? EncryptSensitive(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                var data = Encoding.UTF8.GetBytes(plainText);
                var protectedData = ProtectedData.Protect(
                    data,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(protectedData);
            }
            catch
            {
                return plainText;
            }
        }

                                public static string? DecryptSensitive(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var protectedData = Convert.FromBase64String(cipherText);
                var data = ProtectedData.Unprotect(
                    protectedData,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return cipherText;
            }
        }

                                                public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Пароль не может быть пустым", nameof(password));

            return BCrypt.Net.BCrypt.EnhancedHashPassword(password, Constants.Validation.BcryptWorkFactor);
        }

                                                        public static bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
