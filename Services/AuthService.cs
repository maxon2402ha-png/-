using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    [SupportedOSPlatform("windows")]
    // IDE0290: Используем современный первичный конструктор (Primary Constructor)
    public class AuthService(AppDbContext context)
    {
        private readonly AppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        // Текущий авторизованный пользователь
        public User? CurrentUser { get; private set; }

        // --- ВХОД В СИСТЕМУ ---
        public bool Login(string username, string password)
        {
            try
            {
                username = (username ?? string.Empty).Trim();
                password = (password ?? string.Empty).Trim();

#pragma warning disable CA1862 // Отключаем CA1862: EF Core требует ToLower для трансляции в SQL
#if DEBUG
                // В режиме отладки разрешаем вход под логином admin и паролем admin123
                if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) &&
                    password == "admin123")
                {
                    // Возвращаем ToLower(), чтобы SQL смог это обработать
                    var debugAdmin = _context.Users.AsNoTracking()
                        .FirstOrDefault(u => u.Username.ToLower() == "admin");

                    if (debugAdmin != null)
                    {
                        CurrentUser = debugAdmin;
                        LogSecurityEvent(username, "LoginSuccess", "Вход через Debug Mode");
                        return true;
                    }
                }
#endif

                // Валидация
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    LogSecurityEvent(username, "LoginFailure", "Пустой логин или пароль");
                    return false;
                }

                var normalizedUsername = username.ToLower();

                // Возвращаем ToLower(), чтобы SQL смог это обработать
                var user = _context.Users.AsNoTracking()
                    .FirstOrDefault(u => u.Username.ToLower() == normalizedUsername);
#pragma warning restore CA1862

                if (user == null)
                {
                    LogSecurityEvent(username, "LoginFailure", "Пользователь не найден");
                    return false;
                }

                if (BCrypt.Net.BCrypt.EnhancedVerify(password, user.PasswordHash))
                {
                    // === ПРОВЕРКА EMAIL ВЕРИФИКАЦИИ ===
                    if (!user.IsEmailVerified && user.Role == Constants.UserRoles.Client)
                    {
                        MessageBox.Show("Ваш Email не подтвержден. Пожалуйста, завершите регистрацию.",
                            "Вход запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    // ===================================

                    CurrentUser = user;
                    LogSecurityEvent(user.Username, "LoginSuccess", $"Роль: {user.Role}");
                    return true;
                }

                LogSecurityEvent(username, "LoginFailure", "Неверный пароль");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка авторизации: {ex.Message}");
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public void Logout()
        {
            if (CurrentUser != null)
            {
                LogSecurityEvent(CurrentUser.Username, "Logout", "Выход из системы");
                CurrentUser = null;
            }
        }

        // --- РЕГИСТРАЦИЯ ---

        // CA1822: Метод сделан статическим, так как не использует this
        private static string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Этот метод оставлен для обратной совместимости, но лучше использовать RegisterClientWithCode
        public bool RegisterClient(string username, string password)
        {
            // Упрощенная регистрация без Email (если где-то еще используется)
            return RegisterClientWithCode(username, username, password, "") != null;
        }

        /// <summary>
        /// Регистрирует клиента и возвращает объект User для дальнейшей отправки письма.
        /// </summary>
        public User? RegisterClientWithCode(string name, string username, string password, string email)
        {
            try
            {
                ValidateCredentials(username, password);
                var normalizedUsername = username.Trim().ToLower();

#pragma warning disable CA1862 // Отключаем CA1862: EF Core требует ToLower для трансляции в SQL
                if (_context.Users.Any(u => u.Username.ToLower() == normalizedUsername))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует!");
                    return null;
                }
#pragma warning restore CA1862

                var code = GenerateVerificationCode();

                var user = new User
                {
                    Username = normalizedUsername,
                    PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password.Trim(), Constants.Validation.BcryptWorkFactor),
                    Role = Constants.UserRoles.Client,
                    Email = email,
                    IsEmailVerified = false, // По умолчанию не подтвержден
                    VerificationCode = code
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                var client = new Client
                {
                    Name = name,
                    UserId = user.Id,
                    Email = email
                };

                _context.Clients.Add(client);
                _context.SaveChanges();

                LogSecurityEvent(normalizedUsername, "RegisterClient", "Ожидает подтверждения email");
                return user;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка регистрации: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Проверяет код и активирует аккаунт.
        /// </summary>
        public bool VerifyEmail(int userId, string code)
        {
            var user = _context.Users.Find(userId);
            if (user == null) return false;

            if (user.VerificationCode == code)
            {
                user.IsEmailVerified = true;
                user.VerificationCode = null; // Сбрасываем код безопасности
                _context.SaveChanges();

                LogSecurityEvent(user.Username, "EmailVerified", "Email успешно подтвержден");
                return true;
            }

            return false;
        }

        public bool RegisterEmployee(string name, string username, string password, string role)
        {
            try
            {
                ValidateEmployeeData(name, role);
                ValidateCredentials(username, password);

                var normalizedUsername = username.Trim().ToLower();

#pragma warning disable CA1862 // Отключаем CA1862: EF Core требует ToLower для трансляции в SQL
                if (_context.Users.Any(u => u.Username.ToLower() == normalizedUsername))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует!");
                    return false;
                }
#pragma warning restore CA1862

                var user = new User
                {
                    Username = normalizedUsername,
                    PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password.Trim(), Constants.Validation.BcryptWorkFactor),
                    Role = role.Trim(),
                    IsEmailVerified = true // Сотрудников активируем сразу
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                var employee = new Employee
                {
                    Name = name.Trim(),
                    Role = role.Trim(),
                    UserId = user.Id,
                    MaxActiveTickets = 5
                };

                _context.Employees.Add(employee);
                _context.SaveChanges();

                var creator = CurrentUser?.Username ?? "System";
                LogSecurityEvent(creator, "RegisterEmployee", $"Создан сотрудник: {normalizedUsername} ({role})");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка регистрации сотрудника: {ex.Message}");
                return false;
            }
        }

        // --- УПРАВЛЕНИЕ ПРОФИЛЕМ ---

        public bool UpdateProfile(string newUsername, string? avatarPath)
        {
            if (CurrentUser == null) return false;

            try
            {
                newUsername = newUsername.Trim();

                if (!string.Equals(CurrentUser.Username, newUsername, StringComparison.OrdinalIgnoreCase))
                {
#pragma warning disable CA1862 // Отключаем CA1862: EF Core требует ToLower для трансляции в SQL
                    if (_context.Users.Any(u => u.Username.ToLower() == newUsername.ToLower()))
                    {
                        MessageBox.Show("Этот логин уже занят.");
                        return false;
                    }
#pragma warning restore CA1862
                }

                var userInDb = _context.Users.Find(CurrentUser.Id);
                if (userInDb != null)
                {
                    var oldName = userInDb.Username;
                    userInDb.Username = newUsername;
                    userInDb.AvatarPath = avatarPath;

                    _context.SaveChanges();

                    CurrentUser.Username = newUsername;
                    CurrentUser.AvatarPath = avatarPath;

                    LogSecurityEvent(oldName, "ProfileUpdate", $"Смена логина на {newUsername}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления профиля: {ex.Message}");
                return false;
            }
        }

        public bool ChangePassword(string oldPassword, string newPassword)
        {
            if (CurrentUser == null) return false;

            try
            {
                var userInDb = _context.Users.Find(CurrentUser.Id);
                if (userInDb == null) return false;

                if (!BCrypt.Net.BCrypt.EnhancedVerify(oldPassword, userInDb.PasswordHash))
                {
                    LogSecurityEvent(CurrentUser.Username, "PasswordChangeFailure", "Неверный старый пароль");
                    MessageBox.Show("Старый пароль введен неверно.");
                    return false;
                }

                ValidateCredentials(CurrentUser.Username, newPassword);

                userInDb.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(newPassword, Constants.Validation.BcryptWorkFactor);
                _context.SaveChanges();

                LogSecurityEvent(CurrentUser.Username, "PasswordChangeSuccess", "Пароль успешно изменен");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка смены пароля: {ex.Message}");
                return false;
            }
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

        // CA1822: Метод сделан статическим, так как не использует this
        private static void LogSecurityEvent(string username, string action, string details)
        {
            try
            {
                using var db = App.CreateDbContext();

                db.AuditLogs.Add(new AuditLog
                {
                    Username = username,
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });

                db.SaveChanges();
            }
            catch (Exception ex) { Debug.WriteLine($"Audit Fail: {ex.Message}"); }
        }

        // CA1822: Метод сделан статическим, так как не использует this
        private static void ValidateCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Логин не может быть пустым");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым");

            if (username.Length > Constants.Validation.MaxUsernameLength)
                throw new ArgumentException($"Логин слишком длинный (макс. {Constants.Validation.MaxUsernameLength} символов)");

            if (password.Length < Constants.Validation.MinPasswordLength)
                throw new ArgumentException($"Пароль должен содержать минимум {Constants.Validation.MinPasswordLength} символов");
        }

        // CA1822: Метод сделан статическим, так как не использует this
        private static void ValidateEmployeeData(string name, string role)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя сотрудника не может быть пустым");

            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException("Роль должна быть выбрана");

            if (name.Length > Constants.Validation.MaxEmployeeNameLength)
                throw new ArgumentException($"Имя слишком длинное (макс. {Constants.Validation.MaxEmployeeNameLength} символов)");

            if (role != Constants.UserRoles.Admin && role != Constants.UserRoles.Support && role != Constants.UserRoles.Client)
                throw new ArgumentException($"Недопустимая роль: {role}");
        }
    }
}