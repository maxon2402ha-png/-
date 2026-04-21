using System;
using КР_Ханников.Core;

namespace КР_Ханников.Services
{
    /// <summary>
    /// Маппинг локальных сущностей тикета в поля внешних систем.
    /// </summary>
    public static class IssueMapper
    {
        // --- Статусы ---
        public static string ToJiraStatus(string localStatus)
        {
            if (string.IsNullOrWhiteSpace(localStatus)) return "To Do";

            switch (localStatus.Trim())
            {
                case Constants.TicketStatus.Open: return "To Do";
                case Constants.TicketStatus.InProgress: return "In Progress";
                case Constants.TicketStatus.Closed: return "Done";
                default: return "To Do";
            }
        }

        // --- Приоритеты ---
        public static string ToJiraPriority(TicketPriority priority)
        {
            return priority switch
            {
                TicketPriority.Low => "Low",
                TicketPriority.Normal => "Medium",
                TicketPriority.High => "High",
                TicketPriority.Critical => "Highest",
                _ => "Medium"
            };
        }

        public static string BuildDescription(Ticket t)
        {
            var due = t.DueAt.HasValue ? $"\n\nDue: {t.DueAt:dd.MM.yyyy HH:mm}" : "";
            var who = t.Assignee?.User?.Username != null ? $"\nAssignee: {t.Assignee.User.Username}" : "";
            return $"{t.Description}{who}{due}";
        }

        /// <summary>
        /// Хэш содержимого для отслеживания изменений.
        /// </summary>
        public static string ComputeContentHash(Ticket t)
        {
            var key = $"{t.Title}|{t.Description}|{t.Status}|{t.Priority}|{t.Category}|{t.DueAt:O}|{t.Assignee?.User?.Username}";
            return key.GetHashCode().ToString();
        }
    }
}