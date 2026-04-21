using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Вспомогательные методы для шифрования/расшифровки конфиденциальных данных.
    /// Реализация основана на DPAPI (ProtectedData) и привязана к текущему пользователю Windows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class CryptoHelper
    {
        /// <summary>
        /// Шифрует строку для безопасного хранения.
        /// </summary>
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

        /// <summary>
        /// Расшифровывает строку.
        /// </summary>
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

        /// <summary>
        /// Хеширует пароль с использованием BCrypt.
        /// </summary>
        /// <param name="password">Пароль в открытом виде</param>
        /// <returns>Хеш пароля</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Пароль не может быть пустым", nameof(password));

            return BCrypt.Net.BCrypt.EnhancedHashPassword(password, Constants.Validation.BcryptWorkFactor);
        }

        /// <summary>
        /// Проверяет соответствие пароля его хешу.
        /// </summary>
        /// <param name="password">Пароль в открытом виде</param>
        /// <param name="passwordHash">Хеш пароля из БД</param>
        /// <returns>true если пароль верный</returns>
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
