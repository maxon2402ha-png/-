using System;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Связка локального тикета и внешней задачи.
    /// </summary>
    public class ExternalLink
    {
        public int Id { get; set; }
        public int TicketId { get; set; }

        public ExternalSystem System { get; set; }

        /// <summary> Внешний ID (например, Jira issue id или Trello card id). </summary>
        public string ExternalId { get; set; } = string.Empty;

        /// <summary> Удобный ключ/номер (например, "PROJ-123") — если поддерживается. </summary>
        public string? ExternalKey { get; set; }

        /// <summary> Прямой URL во внешней системе. </summary>
        public string? Url { get; set; }

        /// <summary> В какую сторону синхронизируем. 0=оба, 1=только внаружу, 2=только внутрь. </summary>
        public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;

        public DateTime? LastSyncedAt { get; set; }
        public string? ContentHash { get; set; } // для дешёвой детекции изменений
    }

    public enum SyncDirection
    {
        TwoWay = 0,
        PushOnly = 1,
        PullOnly = 2
    }
}
