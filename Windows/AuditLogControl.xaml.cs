using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class AuditLogControl : UserControl
    {
        private readonly AuthService _authService;
        private readonly DispatcherTimer _searchTimer;

        public AuditLogControl(AppDbContext context, AuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

                        FromPicker.SelectedDate = DateTime.Now.AddDays(-7);
            ToPicker.SelectedDate = DateTime.Now;

                        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchTimer.Tick += async (s, e) =>
            {
                _searchTimer.Stop();
                await LoadLogsAsync();
            };

            Loaded += async (s, e) => await LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            try
            {
                using var db = App.CreateDbContext();

                                var query = db.AuditLogs.AsNoTracking().AsQueryable();

                                var search = SearchBox.Text?.Trim().ToLower();
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(l =>
                        (l.Username != null && l.Username.ToLower().Contains(search)) ||
                        (l.Action != null && l.Action.ToLower().Contains(search)) ||
                        (l.Details != null && l.Details.ToLower().Contains(search))
                    );
                }

                                if (ActionFilter.SelectedItem is ComboBoxItem item && item.Content != null)
                {
                    var action = item.Content.ToString();
                    if (action != "Все действия")
                    {
                        query = query.Where(l => l.Action == action);
                    }
                }

                                if (FromPicker.SelectedDate.HasValue)
                {
                    var start = FromPicker.SelectedDate.Value.ToUniversalTime();
                    query = query.Where(l => l.Timestamp >= start);
                }

                                if (ToPicker.SelectedDate.HasValue)
                {
                    var end = ToPicker.SelectedDate.Value.AddDays(1).ToUniversalTime();
                    query = query.Where(l => l.Timestamp < end);
                }

                                var logs = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Take(1000)
                    .ToListAsync();

                if (AuditGrid != null)
                {
                    AuditGrid.ItemsSource = logs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных аудита: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
                        if (_searchTimer != null)
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }

        private async void Filter_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
                        SearchBox.Text = string.Empty;
            ActionFilter.SelectedIndex = 0;
            FromPicker.SelectedDate = DateTime.Now.AddDays(-7);
            ToPicker.SelectedDate = DateTime.Now;

            await LoadLogsAsync();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var logs = AuditGrid.ItemsSource as IEnumerable<AuditLog>;
            if (logs == null || !logs.Any())
            {
                MessageBox.Show("Нет данных для выгрузки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = $"SecurityAudit_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8);
                    writer.WriteLine("ID;Время;Пользователь;Действие;Детали");

                    foreach (var l in logs)
                    {
                        string user = string.IsNullOrWhiteSpace(l.Username) ? "Система" : l.Username;
                                                string details = l.Details?.Replace(";", ",").Replace("\n", " ").Replace("\r", "") ?? "";

                        writer.WriteLine($"{l.Id};{l.Timestamp:dd.MM.yyyy HH:mm:ss};{user};{l.Action};{details}");
                    }
                    MessageBox.Show("Журнал аудита успешно выгружен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
                        if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.OpenTickets_Click(sender, e);
            }
            else if (Parent is ContentControl cc)
            {
                cc.Content = null;
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
            }
        }
    }
}