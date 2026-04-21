using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    // Класс для хранения решения тикета
    public class Solution
    {
        public int Id { get; set; }

        [Required]
        public string ResolutionText { get; set; } = string.Empty;

        public DateTime ResolutionDate { get; set; } = DateTime.UtcNow;

        // Добавляем связь с базой знаний (опционально)
        public int? KnowledgeArticleId { get; set; }


        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;
    }
}