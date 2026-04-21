using System;
using System.Diagnostics;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    public class AuditService
    {
        private readonly AuthService _auth;

        public AuditService(AuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public void Log(string action, string? details = null)
        {
            try
            {
                using var db = new AppDbContext();

                // Если CurrentUser null, используем "anonymous"
                var username = _auth.CurrentUser?.Username ?? "anonymous";

                var entry = new AuditLog
                {
                    Username = username,
                    Action = action,
                    Details = details ?? string.Empty, // Защита от null при вставке в БД
                    Timestamp = DateTime.UtcNow
                };

                db.AuditLogs.Add(entry);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Audit] Ошибка записи аудита: " + ex);
            }
        }
    }
}