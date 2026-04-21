using System;
using System.ComponentModel.DataAnnotations;

namespace КР_Ханников.Core
{
    public class ResolvedTicket
    {
        [Key]
        public int Id { get; set; }
        public int OriginalTicketId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string ResolvedBy { get; set; } = string.Empty;
        public DateTime ResolutionDate { get; set; }
        public int ClientId { get; set; }
    }
}