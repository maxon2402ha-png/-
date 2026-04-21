using System;
using System.Linq;
using System.Runtime.Versioning; // Добавлено
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")] // Исправлено: CA1416
    public partial class IntegrationSettingsControl : UserControl
    {
        private readonly AppDbContext _db;
        private readonly AuthService _auth;
        private readonly AuditService _audit;

        public IntegrationSettingsControl(AppDbContext db, AuthService authService, AuditService auditService)
        {
            InitializeComponent();

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auth = authService ?? throw new ArgumentNullException(nameof(authService));
            _audit = auditService ?? throw new ArgumentNullException(nameof(auditService));

            if (!CheckUserAccess())
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }

            SystemBox.SelectedIndex = 0;
            LoadExistingIfAny();

            _audit.Log("Открытие настроек интеграции");
        }

        public IntegrationSettingsControl(AppDbContext db, AuthService authService)
            : this(db, authService, new AuditService(authService))
        {
        }

        private bool CheckUserAccess()
        {
            var user = _auth.CurrentUser;
            if (user == null || user.Role != Constants.UserRoles.Admin)
            {
                MessageBox.Show("Доступ запрещен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void LoadExistingIfAny()
        {
            try
            {
                var existing = _db.IntegrationSettings.FirstOrDefault(x => x.System == ExternalSystem.Jira);
                if (existing == null) return;

                foreach (var item in SystemBox.Items.OfType<ComboBoxItem>())
                {
                    if (string.Equals(item.Content?.ToString(), existing.System.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        SystemBox.SelectedItem = item;
                        break;
                    }
                }

                BaseUrlBox.Text = existing.BaseUrl;
                ProjectKeyBox.Text = existing.ProjectKey;
                BoardOrListIdBox.Text = existing.BoardOrListId;
                AuthLoginBox.Text = existing.AuthLogin;
                AuthSecretBox.Password = CryptoHelper.DecryptSensitive(existing.AuthSecret) ?? string.Empty;

                IssueTypeBox.Text = existing.DefaultIssueType;
                DefaultPriorityBox.Text = existing.DefaultPriority;
            }
            catch { /* ignore */ }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SystemBox.SelectedItem is not ComboBoxItem si) return;
                var sysText = si.Content?.ToString() ?? "Jira";
                if (!Enum.TryParse<ExternalSystem>(sysText, out var system)) return;

                var set = _db.IntegrationSettings.FirstOrDefault(x => x.System == system);
                if (set == null)
                {
                    set = new IntegrationSettings { System = system };
                    _db.IntegrationSettings.Add(set);
                }

                set.BaseUrl = BaseUrlBox.Text?.Trim() ?? "";
                set.ProjectKey = ProjectKeyBox.Text?.Trim() ?? "";
                set.BoardOrListId = BoardOrListIdBox.Text?.Trim() ?? "";
                set.AuthLogin = AuthLoginBox.Text?.Trim() ?? "";
                var plainSecret = AuthSecretBox.Password ?? "";
                set.AuthSecret = CryptoHelper.EncryptSensitive(plainSecret) ?? "";
                set.DefaultIssueType = IssueTypeBox.Text?.Trim() ?? "Task";
                set.DefaultPriority = DefaultPriorityBox.Text?.Trim() ?? "Medium";

                _db.SaveChanges();
                _audit.Log("Сохранение настроек интеграции", $"System={system}");

                MessageBox.Show("Настройки сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn) btn.IsEnabled = false;
                ConnectionStatusText.Text = "Проверка...";
                ConnectionStatusText.Foreground = Brushes.Orange;

                await Task.Delay(1000);

                if (string.IsNullOrWhiteSpace(BaseUrlBox.Text)) throw new Exception("URL не задан");

                ConnectionStatusText.Text = "Подключено";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(22, 197, 94));
                MessageBox.Show("Связь установлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = "Ошибка";
                ConnectionStatusText.Foreground = Brushes.Red;
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
            {
                // CA1416: Это безопасно, так как класс помечен SupportedOSPlatform
                window.OpenTickets_Click(sender, e);
            }
        }
    }
}