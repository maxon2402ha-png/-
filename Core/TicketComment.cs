using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
                public class TicketComment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

                        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Author { get; set; } = null!;

        [Required]
        public string Text { get; set; } = string.Empty;

                                        public bool IsInternal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}