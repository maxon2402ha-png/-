using System;

namespace КР_Ханников.Core
{
                public class Notification
    {
        public int Id { get; set; }

                public int UserId { get; set; }
        public User User { get; set; } = null!;

                public string Title { get; set; } = string.Empty;

                public string Message { get; set; } = string.Empty;

                public string Type { get; set; } = "General";

                public int? TicketId { get; set; }
        public Ticket? Ticket { get; set; }

                public DateTime CreatedAt { get; set; }

                public bool IsRead { get; set; }
    }
}