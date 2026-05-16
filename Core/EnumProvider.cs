using System;

namespace КР_Ханников.Core
{
                public static class EnumProvider
    {
        public static Array TicketCategoryValues =>
            Enum.GetValues(typeof(TicketCategory));

        public static Array TicketPriorityValues =>
            Enum.GetValues(typeof(TicketPriority));
    }
}