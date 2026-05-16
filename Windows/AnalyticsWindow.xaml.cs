using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
                        [SupportedOSPlatform("windows")]
    public partial class AnalyticsControl : UserControl
    {
        private List<EmployeeKpi> _employeeKpis = new();
        private List<EmployeeWorkload> _workloads = new();

                public SeriesCollection ForecastSeries { get; } = new();

                public string[] ForecastLabels { get; private set; } = Array.Empty<string>();

        public AnalyticsControl()
        {
            InitializeComponent();

                        DataContext = this;

                        ToDatePicker.SelectedDate = DateTime.Now;
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);

            Loaded += async (s, e) => await LoadAllAsync();
        }

                                private async Task LoadAllAsync()
        {
            await LoadKpiAsync();
            await LoadWorkloadAsync();
            await LoadRebalancingAsync();
            await LoadForecastAsync();
        }

                                private async Task LoadKpiAsync()
        {
            try
            {
                StatusText.Text = "Расчёт KPI...";

                                var fromUtc = (FromDatePicker.SelectedDate?.Date
                               ?? DateTime.Now.AddDays(-30).Date).ToUniversalTime();
                var toUtc = ((ToDatePicker.SelectedDate?.Date ?? DateTime.Now.Date)
                             .AddDays(1).AddSeconds(-1)).ToUniversalTime();

                using var db = App.CreateDbContext();
                var kpiService = new KpiService(db);

                var summary = await kpiService.GetSummaryAsync(fromUtc, toUtc);
                _employeeKpis = await kpiService.GetEmployeeKpisAsync(fromUtc, toUtc);

                                KpiSlaText.Text = $"{summary.SlaCompliancePercent:N1}%";
                KpiFrtText.Text = FormatHours(summary.AvgFirstResponseHours);
                KpiResolutionText.Text = FormatHours(summary.AvgResolutionHours);
                KpiResolvedText.Text = summary.ResolvedTickets.ToString();
                KpiOverdueText.Text = $"{summary.OverdueRate:N1}%";

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

                                        private async Task LoadWorkloadAsync()
        {
            try
            {
                using var db = App.CreateDbContext();
                var workloadService = new WorkloadService(db);

                var summary = await workloadService.GetSummaryAsync();
                _workloads = await workloadService.GetWorkloadAsync();

                                WlAvgText.Text = $"{summary.AvgLoadPercent:N0}%";
                WlOverloadedText.Text = summary.OverloadedCount.ToString();
                WlActiveText.Text = summary.TotalActiveTickets.ToString();
                WlEmployeesText.Text = summary.TotalEmployees.ToString();

                                WorkloadGrid.ItemsSource = _workloads;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчёта нагрузки:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                                        private async Task LoadRebalancingAsync()
        {
            try
            {
                using var db = App.CreateDbContext();
                var rebalancingService = new RebalancingService(db);

                var result = await rebalancingService.GetRecommendationsAsync();

                if (result.HasRecommendations)
                {
                    RebalanceHeaderText.Text =
                        $"Перегружено операторов: {result.OverloadedCount}. " +
                        $"Рекомендуется передать тикетов: {result.Recommendations.Count}";
                    RebalanceGrid.ItemsSource = result.Recommendations;
                    RebalanceGrid.Visibility = Visibility.Visible;
                    RebalanceEmptyText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RebalanceHeaderText.Text = "Анализ перераспределения тикетов";
                    RebalanceGrid.ItemsSource = null;
                    RebalanceGrid.Visibility = Visibility.Collapsed;
                    RebalanceEmptyText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка анализа перераспределения:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                                        private async void ApplyRebalance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not RebalancingRecommendation rec)
                return;

            var confirm = MessageBox.Show(
                $"Передать тикет #{rec.TicketId} «{rec.TicketTitle}»\n" +
                $"от {rec.FromEmployeeName} к {rec.ToEmployeeName}?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = App.CreateDbContext();
                var rebalancingService = new RebalancingService(db);

                bool ok = await rebalancingService.ApplyReassignmentAsync(
                    rec.TicketId, rec.ToEmployeeId, "Аналитика KPI");

                if (ok)
                {
                    StatusText.Text = $"Тикет #{rec.TicketId} переназначен";
                                        await LoadWorkloadAsync();
                    await LoadRebalancingAsync();
                }
                else
                {
                    MessageBox.Show("Не удалось переназначить тикет.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка переназначения:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                                        private async Task LoadForecastAsync()
        {
            try
            {
                using var db = App.CreateDbContext();
                var forecastService = new ForecastService(db);

                const int daysAhead = 7;
                var forecast = await forecastService.ForecastTicketInflowAsync(daysAhead);
                var risk = await forecastService.AssessSlaRiskAsync(daysAhead);

                                if (!forecast.HasEnoughData)
                {
                    FcInflowText.Text = "—";
                    FcBacklogText.Text = "—";
                    FcRatioText.Text = "—";
                    FcRiskText.Text = "Недостаточно данных";
                }
                else
                {
                    FcInflowText.Text = forecast.TotalPredicted.ToString("N0");
                    FcBacklogText.Text = risk.CurrentBacklog.ToString();
                    FcRatioText.Text = risk.LoadRatio.ToString("N2");
                    FcRiskText.Text = risk.RiskText;
                }

                                ForecastSeries.Clear();
                ForecastLabels = forecast.Points
                    .Select(p => p.DateLabel)
                    .ToArray();

                ForecastSeries.Add(new LineSeries
                {
                    Title = "Прогноз тикетов",
                    Values = new ChartValues<double>(
                        forecast.Points.Select(p => p.PredictedTickets)),
                    DataLabels = true,
                                        LabelPoint = point => ((int)Math.Round(point.Y)).ToString(),
                    LineSmoothness = 0.5
                });

                if (ForecastChart != null)
                {
                    ForecastChart.AxisX[0].Labels = ForecastLabels;

                                                                                                    double maxValue = forecast.Points.Count > 0
                        ? forecast.Points.Max(p => p.PredictedTickets)
                        : 0;

                    if (maxValue <= 0)
                    {
                        ForecastChart.AxisY[0].MinValue = 0;
                        ForecastChart.AxisY[0].MaxValue = 5;
                    }
                    else
                    {
                        ForecastChart.AxisY[0].MinValue = 0;
                        ForecastChart.AxisY[0].MaxValue = double.NaN;                     }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка прогноза:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                                private static string FormatHours(double hours)
        {
            if (hours <= 0) return "—";
            return hours < 24
                ? $"{hours:N1} ч"
                : $"{hours / 24:N1} дн";
        }

        
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
    }
}