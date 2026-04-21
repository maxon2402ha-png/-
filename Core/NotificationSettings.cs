namespace КР_Ханников.Core
{
    /// <summary>
    /// Личные настройки уведомлений для пользователя.
    /// </summary>
    public class NotificationSettings
    {
        public int Id { get; set; }

        /// <summary>Пользователь, к которому относятся настройки</summary>
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>Оповещать ли о новых обращениях (для Support / Admin)</summary>
        public bool NotifyOnNewTickets { get; set; } = true;

        /// <summary>Оповещать ли об изменении статуса тикетов</summary>
        public bool NotifyOnStatusChanged { get; set; } = true;

        /// <summary>Оповещать ли о новых комментариях</summary>
        public bool NotifyOnComments { get; set; } = true;

        /// <summary>Оповещать ли о приближении сроков</summary>
        public bool NotifyOnDueSoon { get; set; } = true;

        /// <summary>
        /// За сколько минут до срока считать, что "скоро истекает".
        /// По умолчанию — 60 минут.
        /// </summary>
        public int DueSoonThresholdMinutes { get; set; } = 60;

        /// <summary>Воспроизводить звук при уведомлении</summary>
        public bool PlaySound { get; set; } = true;

        /// <summary>Показывать всплывающие уведомления (Toast)</summary>
        public bool ShowToast { get; set; } = true;
    }
}
