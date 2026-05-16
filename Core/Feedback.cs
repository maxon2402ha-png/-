using System;

namespace КР_Ханников.Core
{
                public class Feedback
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public int ClientId { get; set; }
        public Client Client { get; set; } = null!;

                                public int? SupportId { get; set; }
        public Employee? Support { get; set; }

                                public string Comment { get; set; } = string.Empty;

                                public int Rating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}