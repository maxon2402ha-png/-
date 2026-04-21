using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    public class Ticket
    {
        /// <summary>
        /// Допустимые строковые статусы тикета.
        /// Используется, например, в TicketService.ChangeStatusAsync.
        /// </summary>
        public static readonly string[] AllStatuses =
        {
            Constants.TicketStatus.Open,
            Constants.TicketStatus.InProgress,
            Constants.TicketStatus.Closed
        };

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Статус тикета (строка: Open / InProgress / Closed и т.п.).
        /// Значения централизованы в Constants.TicketStatus.
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; } = Constants.TicketStatus.Open;

        /// <summary>
        /// Приоритет тикета (enum).
        /// Хранится в БД как int.
        /// </summary>
        public TicketPriority Priority { get; set; } = TicketPriority.Normal;

        /// <summary>
        /// Категория тикета (enum).
        /// Хранится в БД как int.
        /// </summary>
        public TicketCategory Category { get; set; } = TicketCategory.General;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        /// <summary>
        /// Путь к файлу вложения (если есть).
        /// </summary>
        [MaxLength(500)]
        public string? AttachmentPath { get; set; }

        /// <summary>
        /// Краткое имя файла для отображения в интерфейсе.
        /// </summary>
        [MaxLength(255)]
        public string? AttachmentFileName { get; set; }

        /// <summary>
        /// MIME-тип вложения (pdf, png и т.д.).
        /// </summary>
        [MaxLength(100)]
        public string? AttachmentContentType { get; set; }

        /// <summary>
        /// Размер файла в байтах (опционально).
        /// </summary>
        public long? AttachmentSize { get; set; }

        /// <summary>
        /// Связанный сотрудник (исполнитель).
        /// </summary>
        public int? AssigneeEmployeeId { get; set; }
        public Employee? Assignee { get; set; }

        /// <summary>
        /// История изменений тикета (создание, смена статуса, дедлайнов и т.п.).
        /// </summary>
        public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();

        /// <summary>
        /// Решение по тикету (заполняется при закрытии).
        /// </summary>
        public Solution? Solution { get; set; }

        /// <summary>
        /// Отзыв клиента по закрытому тикету.
        /// </summary>
        public Feedback? Feedback { get; set; }

        /// <summary>
        /// Комментарии по тикету (внутренние/внешние).
        /// </summary>
        public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

        // Внешние ключи
        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // опциональная связь с пользователем-создателем (кто завёл тикет)
        public int? UserId { get; set; }
        public User? User { get; set; }

        /// <summary>
        /// Флаг просрочки для подсветки строк в DataGrid.
        /// </summary>
        [NotMapped]
        public bool IsOverdue =>
            DueAt.HasValue &&
            DueAt.Value < DateTime.UtcNow &&
            Status != Constants.TicketStatus.Closed;
    }
}