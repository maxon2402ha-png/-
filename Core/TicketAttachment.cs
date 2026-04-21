using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Вложенный файл тикета, если храним файл целиком в БД.
    /// Для курсовой можно использовать как альтернативу хранению по пути.
    /// </summary>
    public class TicketAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public byte[] FileData { get; set; } = Array.Empty<byte>(); // содержимое файла

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Внешний ключ
        public int TicketId { get; set; }

        // Навигационное свойство
        public Ticket Ticket { get; set; } = null!;
    }
}