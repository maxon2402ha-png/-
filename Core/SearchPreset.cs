using System;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Сохранённый поисковый запрос / фильтр.
    /// Привязан к конкретному пользователю.
    /// </summary>
    public class SearchPreset
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Название сохранённого поиска (то, что видит пользователь в комбобоксе).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Текстовый запрос (поиск по тексту).</summary>
        public string? TextQuery { get; set; }

        /// <summary>Фильтр по статусу ("Open", "In Progress", "Closed"). null = все.</summary>
        public string? Status { get; set; }

        /// <summary>Фильтр по категории тикета.</summary>
        public TicketCategory? Category { get; set; }

        /// <summary>Фильтр по приоритету тикета.</summary>
        public TicketPriority? Priority { get; set; }

        /// <summary>Дата создания "с".</summary>
        public DateTime? CreatedFrom { get; set; }

        /// <summary>Дата создания "по".</summary>
        public DateTime? CreatedTo { get; set; }
    }
}