using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
                public class UserUiSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        
                public bool ShowKpiBlock { get; set; } = true;

                public bool ShowChartsBlock { get; set; } = true;

                public bool ShowDetailedTable { get; set; } = true;

                public int DefaultPeriodDays { get; set; } = 30;

                public int RefreshRateSeconds { get; set; } = 30;

        
                [MaxLength(20)]
        public string Theme { get; set; } = "Light";

        
                [NotMapped]
        public bool ShowKpi
        {
            get => ShowKpiBlock;
            set => ShowKpiBlock = value;
        }

                [NotMapped]
        public bool ShowCharts
        {
            get => ShowChartsBlock;
            set => ShowChartsBlock = value;
        }

                [NotMapped]
        public bool ShowTable
        {
            get => ShowDetailedTable;
            set => ShowDetailedTable = value;
        }

                [NotMapped]
        public int RefreshIntervalSeconds
        {
            get => RefreshRateSeconds;
            set => RefreshRateSeconds = value;
        }

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
