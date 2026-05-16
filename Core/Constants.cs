using System;
using System.IO;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Централизованное хранилище всех констант приложения.
    /// </summary>
    public static class Constants
    {
        public static class TicketStatus
        {
            public const string Open = "Open";
            public const string InProgress = "In Progress";
            public const string Resolved = "Resolved";
            public const string Closed = "Closed";

            public static readonly string[] All = { Open, InProgress, Resolved, Closed };

            public static bool IsValid(string status)
                => Array.Exists(All, s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        public static class UserRoles
        {
            public const string Admin = "Admin";
            public const string Support = "Support";
            public const string Client = "Client";

            public static readonly string[] All = { Admin, Support, Client };

            public static bool IsValid(string role)
                => Array.Exists(All, r => r.Equals(role, StringComparison.OrdinalIgnoreCase));

            public static bool IsEmployee(string role)
                => role == Admin || role == Support;
        }

        /// <summary>
        /// Настройки подключения к PostgreSQL.
        /// </summary>
        public static class Database
        {
            // === НАСТРОЙКИ СЕРВЕРА ===
            private const string Host = "localhost";
            private const string Port = "5432";

            // ИСПРАВЛЕНИЕ: Изменили имя БД на _v2, чтобы создать новую чистую базу со всеми колонками
            private const string DbName = "TicketSystemDb"; // Новое имя

            private const string User = "postgres";

            // !!! ВНИМАНИЕ: Впиши сюда свой пароль, который вводил в pgAdmin !!!
            private const string Password = "qwerty";

            public static string GetConnectionString()
            {
                return $"Host={Host};Port={Port};Database={DbName};Username={User};Password={Password}";
            }

            public const string ApplicationFolderName = "КР_Ханников";
            public const string LogsFolderName = "logs";

            public static string GetLogsPath()
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ApplicationFolderName,
                    LogsFolderName
                );
            }

            // Заглушка для совместимости
            public static string GetDatabasePath() => "";
        }

        public static class Validation
        {
            public const int MinPasswordLength = 8;
            public const int MaxUsernameLength = 50;
            public const int MaxTicketTitleLength = 200;
            public const int MaxEmployeeNameLength = 100;
            public const int BcryptWorkFactor = 13;
        }

        public static class NotificationTypes
        {
            public const string TicketAssigned = "TicketAssigned";
            public const string TicketUpdated = "TicketUpdated";
            public const string TicketClosed = "TicketClosed";
            public const string TicketComment = "TicketComment";
            public const string TicketDueSoon = "TicketDueSoon";
            public const string TicketOverdue = "TicketOverdue";
            public const string SystemAlert = "SystemAlert";
        }

        public static class Integration
        {
            public const int HttpTimeoutSeconds = 30;
            public const int MaxRetryAttempts = 3;
            public const int RetryDelaySeconds = 2;
        }

        public static class UI
        {
            public const int DefaultPageSize = 50;
            public const int NotificationCheckIntervalMinutes = 2;
            public const int AutoSaveIntervalMinutes = 5;
        }
    }
}