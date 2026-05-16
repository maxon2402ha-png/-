using System;

namespace КР_Ханников.Core
{
                public class SearchPreset
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

                public string Name { get; set; } = string.Empty;

                public string? TextQuery { get; set; }

                public string? Status { get; set; }

                public TicketCategory? Category { get; set; }

                public TicketPriority? Priority { get; set; }

                public DateTime? CreatedFrom { get; set; }

                public DateTime? CreatedTo { get; set; }
    }
}