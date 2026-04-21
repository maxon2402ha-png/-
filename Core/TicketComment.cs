using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Комментарий к тикету.
    /// </summary>
    public class TicketComment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        // ИСПРАВЛЕНО: Ссылка теперь на User, а не на Employee
        // Это позволяет писать комментарии и Клиентам, и Поддержке
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Author { get; set; } = null!;

        [Required]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// true — внутренний комментарий (виден только сотрудникам).
        /// false — внешний (виден клиенту).
        /// </summary>
        public bool IsInternal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}