using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class AgentDashboardControl : UserControl
    {
        private readonly AuthService _authService;

        public AgentDashboardControl(AuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            Loaded += async (s, e) => await LoadDashboardDataAsync();
        }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                var user = _authService.CurrentUser;
                if (user == null || user.Role != Constants.UserRoles.Support) return;

                using var db = App.CreateDbContext();
                var kpiService = new KpiService(db);

                                var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return;

                GreetingText.Text = $"Привет, {employee.Name.Split(' ')[0]}!";

                                int currentLoad = await kpiService.CalculateEmployeeWorkloadAsync(employee.Id);
                WorkloadProgress.Value = currentLoad;
                WorkloadPointsText.Text = $"{currentLoad} / {KpiService.MaxWorkloadPoints} баллов";

                                if (currentLoad < 10)
                {
                    WorkloadProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));                     WorkloadStatusText.Text = "Оптимальная нагрузка";
                    WorkloadStatusText.Foreground = WorkloadProgress.Foreground;
                }
                else if (currentLoad < 16)
                {
                    WorkloadProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));                     WorkloadStatusText.Text = "Повышенная нагрузка";
                    WorkloadStatusText.Foreground = WorkloadProgress.Foreground;
                }
                else
                {
                    WorkloadProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));                     WorkloadStatusText.Text = "Критический перегруз!";
                    WorkloadStatusText.Foreground = WorkloadProgress.Foreground;
                }

                                var fromDate = DateTime.UtcNow.AddDays(-7);
                var toDate = DateTime.UtcNow;

                double sla = await kpiService.CalculateSlaComplianceAsync(employee.Id, fromDate, toDate);
                double art = await kpiService.CalculateArtAsync(employee.Id, fromDate, toDate);

                SlaText.Text = $"{sla:0.#}%";
                if (sla < 90) SlaText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); 
                ArtText.Text = $"{art:0.#}";

                                var activeTickets = await db.Tickets
                    .AsNoTracking()
                    .Where(t => t.AssigneeEmployeeId == employee.Id && t.Status != Constants.TicketStatus.Closed && t.Status != Constants.TicketStatus.Resolved)
                    .OrderBy(t => t.DueAt)
                    .ToListAsync();

                MyTicketsGrid.ItemsSource = activeTickets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дашборда: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}