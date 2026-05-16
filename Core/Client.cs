using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    public class Client
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Отключаем поиск этих колонок в базе данных
        [NotMapped]
        public string Email { get; set; } = string.Empty;

        [NotMapped]
        public string Company { get; set; } = string.Empty;

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}