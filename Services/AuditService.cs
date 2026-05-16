using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    [SupportedOSPlatform("windows")]
        public class AuditService(AuthService auth)
    {
        private readonly AuthService _auth = auth ?? throw new ArgumentNullException(nameof(auth));

        public void Log(string action, string? details = null)
        {
            try
            {
                using var db = new AppDbContext();

                var username = _auth.CurrentUser?.Username ?? "anonymous";

                var entry = new AuditLog
                {
                    Username = username,
                    Action = action,
                    Details = details ?? string.Empty,
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