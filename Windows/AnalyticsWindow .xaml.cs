using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    /// <summary>
    /// Окно аналитики: мониторинг KPI сотрудников и анализ нагрузки операторов.
    /// Использует KpiService и WorkloadService для расчётов.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class AnalyticsWindow : Window
    {
        private List<EmployeeKpi> _employeeKpis = new();
        private List<EmployeeWorkload> _workloads = new();

        public AnalyticsWindow()
        {
            InitializeComponent();

            // Период по умолчанию — последние 30 дней.
            ToDatePicker.SelectedDate = DateTime.Now;
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);

            Loaded += async (s, e) => await LoadAllAsync();
        }

        /// <summary>
        /// Загружает данные для обеих вкладок: KPI и нагрузку.
        /// </summary>
        private async Task LoadAllAsync()
        {
            await LoadKpiAsync();
            await LoadWorkloadAsync();
        }

        /// <summary>
        /// Рассчитывает и отображает KPI сотрудников за выбранный период.
        /// </summary>
        private async Task LoadKpiAsync()
        {
            try
            {
                StatusText.Text = "Расчёт KPI...";

                // Границы периода. Конечную дату берём включительно (до конца суток).
                var fromUtc = (FromDatePicker.SelectedDate?.Date
                               ?? DateTime.Now.AddDays(-30).Date).ToUniversalTime();
                var toUtc = ((ToDatePicker.SelectedDate?.Date ?? DateTime.Now.Date)
                             .AddDays(1).AddSeconds(-1)).ToUniversalTime();

                using var db = App.CreateDbContext();
                var kpiService = new KpiService(db);

                var summary = await kpiService.GetSummaryAsync(fromUtc, toUtc);
                _employeeKpis = await kpiService.GetEmployeeKpisAsync(fromUtc, toUtc);

                // Сводные карточки.
                KpiSlaText.Text = $"{summary.SlaCompliancePercent:N1}%";
                KpiFrtText.Text = FormatHours(summary.AvgFirstResponseHours);
                KpiResolutionText.Text = FormatHours(summary.AvgResolutionHours);
                KpiResolvedText.Text = summary.ResolvedTickets.ToString();
                KpiOverdueText.Text = $"{summary.OverdueRate:N1}%";

                // Таблица по сотрудникам.
                KpiGrid.ItemsSource = _employeeKpis;

                StatusText.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка расчёта KPI";
                MessageBox.Show($"Ошибка расчёта KPI:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Рассчитывает и отображает текущую нагрузку операторов.
        /// Нагрузка считается по активным тикетам и от периода не зависит.
        /// </summary>
        private async Task LoadWorkloadAsync()
        {
            try
            {
                using var db = App.CreateDbContext();
                var workloadService = new WorkloadService(db);

                var summary = await workloadService.GetSummaryAsync();
                _workloads = await workloadService.GetWorkloadAsync();

                // Сводные карточки.
                WlAvgText.Text = $"{summary.AvgLoadPercent:N0}%";
                WlOverloadedText.Text = summary.OverloadedCount.ToString();
                WlActiveText.Text = summary.TotalActiveTickets.ToString();
                WlEmployeesText.Text = summary.TotalEmployees.ToString();

                // Таблица нагрузки.
                WorkloadGrid.ItemsSource = _workloads;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчёта нагрузки:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Форматирует время: до 24 часов — в часах, дольше — в днях.
        /// </summary>
        private static string FormatHours(double hours)
        {
            if (hours <= 0) return "—";
            return hours < 24
                ? $"{hours:N1} ч"
                : $"{hours / 24:N1} дн";
        }

        // --- Обработчики кнопок ---

        private async void Apply_Click(object sender, RoutedEventArgs e)
            => await LoadAllAsync();

        private async void Quick30_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Now;
            await LoadAllAsync();
        }

        private async void Quick90_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-90);
            ToDatePicker.SelectedDate = DateTime.Now;
            await LoadAllAsync();
        }

        /// <summary>
        /// Экспортирует данные обеих таблиц в один CSV-файл.
        /// </summary>
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_employeeKpis.Count == 0 && _workloads.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV файл|*.csv",
                FileName = $"Analytics_{DateTime.Now:yyyyMMdd}.csv",
                Title = "Сохранить аналитику"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("=== KPI СОТРУДНИКОВ ===");
                sb.AppendLine("Сотрудник;Логин;Тикетов;Решено;SLA %;Ответ ч;Решение ч;Просрочка %;Оценка");
                foreach (var k in _employeeKpis)
                {
                    sb.AppendLine(
                        $"{k.Name};{k.Username};{k.TotalTickets};{k.ResolvedTickets};" +
                        $"{k.SlaCompliancePercent:F1};{k.AvgFirstResponseHours:F1};" +
                        $"{k.AvgResolutionHours:F1};{k.OverdueRate:F1};{k.AvgRating:F1}");
                }

                sb.AppendLine();
                sb.AppendLine("=== НАГРУЗКА ОПЕРАТОРОВ ===");
                sb.AppendLine("Оператор;Логин;Активных;Критичных;Вес. нагрузка;Лимит;Загрузка %;Статус");
                foreach (var w in _workloads)
                {
                    sb.AppendLine(
                        $"{w.Name};{w.Username};{w.ActiveTickets};{w.CriticalCount};" +
                        $"{w.WeightedLoad};{w.MaxActiveTickets};{w.LoadPercent:F1};{w.LevelText}");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"Файл сохранён:\n{dialog.FileName}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}