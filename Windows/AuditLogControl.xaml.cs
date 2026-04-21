using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.Versioning; // Добавлено
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")] // Исправлено: CA1416
    public partial class AuditLogControl : UserControl
    {
        private readonly AppDbContext _context;
        private readonly AuthService _auth;

        public AuditLogControl(AppDbContext context, AuthService auth)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            if (_auth.CurrentUser?.Role != Core.Constants.UserRoles.Admin)
            {
                MessageBox.Show("Доступ запрещен.", "Безопасность", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Visibility = Visibility.Collapsed;
                return;
            }

            FromPicker.SelectedDate = DateTime.Now.AddDays(-7);
            ToPicker.SelectedDate = DateTime.Now;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var query = _context.AuditLogs.AsNoTracking().AsQueryable();

                if (FromPicker.SelectedDate.HasValue)
                    query = query.Where(x => x.Timestamp >= FromPicker.SelectedDate.Value.ToUniversalTime());

                if (ToPicker.SelectedDate.HasValue)
                    query = query.Where(x => x.Timestamp <= ToPicker.SelectedDate.Value.AddDays(1).ToUniversalTime());

                var text = SearchBox.Text?.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    query = query.Where(x => x.Username.ToLower().Contains(text) ||
                                             x.Action.ToLower().Contains(text) ||
                                             x.Details.ToLower().Contains(text));
                }

                var logs = query.OrderByDescending(x => x.Timestamp).Take(1000).ToList();
                AuditGrid.ItemsSource = logs;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e) => LoadData();

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            FromPicker.SelectedDate = DateTime.Now.AddDays(-7);
            ToPicker.SelectedDate = DateTime.Now;
            LoadData();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"AuditLog_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var logs = AuditGrid.ItemsSource as System.Collections.Generic.List<Core.AuditLog>;
                    if (logs == null) return;

                    var sb = new StringBuilder();
                    sb.AppendLine("ID;Date(UTC);User;Action;Details");
                    foreach (var l in logs)
                    {
                        sb.AppendLine($"{l.Id};{l.Timestamp};{l.Username};{l.Action};\"{l.Details?.Replace("\"", "\"\"")}\"");
                    }
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Экспорт завершен.");
                }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
            {
                // CA1416: Вызов безопасен
                window.OpenTickets_Click(sender, e);
            }
        }
    }
}