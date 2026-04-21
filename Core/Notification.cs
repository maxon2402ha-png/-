using System;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Уведомление, сохраняется в БД.
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        /// <summary>Кому адресовано уведомление (User.Id)</summary>
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>Краткий заголовок</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Текст уведомления</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Тип уведомления (NewTicket / StatusChanged / DueSoon и т.д.)</summary>
        public string Type { get; set; } = "General";

        /// <summary>Связанный тикет (если есть)</summary>
        public int? TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        /// <summary>Когда создано уведомление</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Прочитал пользователь или нет</summary>
        public bool IsRead { get; set; }
    }
}