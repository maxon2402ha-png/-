using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Персональные настройки интерфейса пользователя.
    /// </summary>
    public class UserUiSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // --- Настройки Дашборда ---

        /// <summary>Показывать блок с цифрами (KPI)</summary>
        public bool ShowKpiBlock { get; set; } = true;

        /// <summary>Показывать графики динамики и диаграммы</summary>
        public bool ShowChartsBlock { get; set; } = true;

        /// <summary>Показывать детальную таблицу внизу</summary>
        public bool ShowDetailedTable { get; set; } = true;

        /// <summary>Период по умолчанию (в днях), например 7, 30, 365</summary>
        public int DefaultPeriodDays { get; set; } = 30;

        /// <summary>Частота автообновления (в секундах). 0 = выкл.</summary>
        public int RefreshRateSeconds { get; set; } = 30;

        // --- ОБЩИЕ НАСТРОЙКИ ---

        /// <summary>Тема оформления (Light / Dark)</summary>
        [MaxLength(20)]
        public string Theme { get; set; } = "Light";

        // --- Алиасы для обратной совместимости ---

        /// <summary>Алиас для ShowKpiBlock</summary>
        [NotMapped]
        public bool ShowKpi
        {
            get => ShowKpiBlock;
            set => ShowKpiBlock = value;
        }

        /// <summary>Алиас для ShowChartsBlock</summary>
        [NotMapped]
        public bool ShowCharts
        {
            get => ShowChartsBlock;
            set => ShowChartsBlock = value;
        }

        /// <summary>Алиас для ShowDetailedTable</summary>
        [NotMapped]
        public bool ShowTable
        {
            get => ShowDetailedTable;
            set => ShowDetailedTable = value;
        }

        /// <summary>Алиас для RefreshRateSeconds</summary>
        [NotMapped]
        public int RefreshIntervalSeconds
        {
            get => RefreshRateSeconds;
            set => RefreshRateSeconds = value;
        }

        /// <summary>Авто-обновление включено (RefreshRateSeconds > 0)</summary>
        [NotMapped]
        public bool AutoRefresh
        {
            get => RefreshRateSeconds > 0;
            set
            {
                if (!value) RefreshRateSeconds = 0;
                else if (RefreshRateSeconds == 0) RefreshRateSeconds = 30;
            }
        }
    }
}
