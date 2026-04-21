using System;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Помощник для биндинга значений enum в XAML.
    /// </summary>
    public static class EnumProvider
    {
        public static Array TicketCategoryValues =>
            Enum.GetValues(typeof(TicketCategory));

        public static Array TicketPriorityValues =>
            Enum.GetValues(typeof(TicketPriority));
    }
}