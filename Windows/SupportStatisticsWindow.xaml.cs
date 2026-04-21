using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class SupportStatisticsWindow : Window
    {
        private List<EmployeeStatistics> _statistics = new();

        public SupportStatisticsWindow()
        {
            InitializeComponent();
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            try
            {
                using var db = new AppDbContext();

                // 1. Load all employees (Support and Admin)
                var employees = db.Employees
                    .Include(e => e.User)
                    .AsNoTracking()
                    .Where(e => e.User.Role == Constants.UserRoles.Support || e.User.Role == Constants.UserRoles.Admin)
                    .ToList();

                // 2. Load tickets assigned to employees, including feedback
                var allTickets = db.Tickets
                    .Include(t => t.Feedback)
                    .AsNoTracking()
                    .Where(t => t.AssigneeEmployeeId != null)
                    .ToList();

                _statistics = new List<EmployeeStatistics>();

                foreach (var emp in employees)
                {
                    // Tickets for this employee
                    var empTickets = allTickets.Where(t => t.AssigneeEmployeeId == emp.Id).ToList();

                    // Count resolved (Status = Closed)
                    int resolvedCount = empTickets.Count(t => t.Status == Constants.TicketStatus.Closed);

                    // Feedback for this employee's tickets
                    var feedbacks = empTickets
                        .Where(t => t.Feedback != null)
                        .Select(t => t.Feedback!)
                        .ToList();

                    int feedbackCount = feedbacks.Count;

                    // Average rating for the employee
                    double avgRating = feedbackCount > 0
                        ? feedbacks.Average(f => f.Rating)
                        : 0.0;

                    _statistics.Add(new EmployeeStatistics
                    {
                        Id = emp.Id,
                        Name = emp.Name,
                        Username = emp.User?.Username ?? "—",
                        ResolvedCount = resolvedCount,
                        FeedbackCount = feedbackCount,
                        AverageRating = avgRating
                    });
                }

                // Sort: first by Rating (descending), then by Resolved Count (descending)
                _statistics = _statistics
                    .OrderByDescending(s => s.AverageRating)
                    .ThenByDescending(s => s.ResolvedCount)
                    .ToList();

                // Assign positions (1, 2, 3...)
                for (int i = 0; i < _statistics.Count; i++)
                {
                    _statistics[i].Position = i + 1;
                }

                // Bind to the DataGrid
                if (StatsGrid != null)
                    StatsGrid.ItemsSource = _statistics;

                // Update KPI cards at the top
                UpdateKpiCards();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateKpiCards()
        {
            // 1. Average rating across all employees (with feedback)
            double totalAvg = 0;
            var ratedEmployees = _statistics.Where(s => s.FeedbackCount > 0).ToList();
            if (ratedEmployees.Any())
            {
                totalAvg = ratedEmployees.Average(s => s.AverageRating);
            }

            if (AvgRatingText != null)
                AvgRatingText.Text = $"{totalAvg:N1}";

            // 2. Total feedback count
            int totalFeedback = _statistics.Sum(s => s.FeedbackCount);
            if (TotalFeedbackText != null)
                TotalFeedbackText.Text = totalFeedback.ToString();

            // 3. Total employees in the ranking
            if (TotalEmployeesText != null)
                TotalEmployeesText.Text = _statistics.Count.ToString();

            // 4. Leader (first in the sorted list)
            var leader = _statistics.FirstOrDefault();
            if (TopEmployeeText != null)
                TopEmployeeText.Text = leader?.Name ?? "—";
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_statistics == null || !_statistics.Any())
            {
                MessageBox.Show("Нет данных для экспорта", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV файл|*.csv",
                    FileName = $"Stats_{DateTime.Now:yyyyMMdd}.csv",
                    Title = "Сохранить статистику"
                };

                if (dialog.ShowDialog() != true)
                    return;

                using var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8);

                // Headers
                writer.WriteLine("Position;Name;Username;Rating;Feedbacks;Resolved");

                // Data
                foreach (var stat in _statistics)
                {
                    writer.WriteLine($"{stat.Position};{stat.Name};{stat.Username};{stat.AverageRating:F1};{stat.FeedbackCount};{stat.ResolvedCount}");
                }

                MessageBox.Show($"Файл сохранен успешно:\n{dialog.FileName}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Data model for statistics row (ViewModel)
    /// </summary>
    public class EmployeeStatistics
    {
        public int Position { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int FeedbackCount { get; set; }
        public int ResolvedCount { get; set; }

        // Calculated property for progress bar width in XAML (max width ~80px or %)
        public double RatingPercent => (AverageRating / 5.0) * 100.0;
    }
}