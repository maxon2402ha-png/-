using System;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Отзыв клиента по тикету (оценка и текстовый комментарий).
    /// </summary>
    public class Feedback
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        /// <summary>
        /// Сотрудник поддержки, который решал тикет.
        /// Может быть null, если не определён.
        /// </summary>
        public int? SupportId { get; set; }
        public Employee? Support { get; set; }

        /// <summary>
        /// Текстовый комментарий клиента.
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Оценка, например, по шкале 1–5.
        /// </summary>
        public int Rating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}