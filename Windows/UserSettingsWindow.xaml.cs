using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Helpers;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class UserSettingsWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly AuthService? _auth;
        private readonly int _userId;
        private UserUiSettings _settings = null!;

        public UserSettingsWindow(AppDbContext context, AuthService auth)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _userId = _auth.CurrentUser?.Id ?? 0;

            LoadSettings();
        }

        public UserSettingsWindow(AppDbContext context)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auth = null;
            _userId = 0;

            LoadSettings();
        }

        public UserSettingsWindow(int userId)
        {
            InitializeComponent();
            _context = new AppDbContext();
            _auth = null;
            _userId = userId;

            LoadSettings();
        }

        private void LoadSettings()
        {
            int userId = _userId;

            if (userId == 0 && _auth?.CurrentUser != null)
            {
                userId = _auth.CurrentUser.Id;
            }

            if (userId == 0)
            {
                MessageBox.Show("Не удалось определить пользователя", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            try
            {
                _settings = _context.UserUiSettings.FirstOrDefault(s => s.UserId == userId)
                            ?? new UserUiSettings { UserId = userId };

                if (_settings.Id == 0)
                {
                    _context.UserUiSettings.Add(_settings);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}");
                _settings = new UserUiSettings { UserId = userId };
            }

                        ShowKpiCheck.IsChecked = _settings.ShowKpiBlock;
            ShowChartsCheck.IsChecked = _settings.ShowChartsBlock;
            ShowTableCheck.IsChecked = _settings.ShowDetailedTable;

                        bool isAuto = _settings.RefreshRateSeconds > 0;
            AutoRefreshCheck.IsChecked = isAuto;
            RefreshSlider.Value = isAuto ? _settings.RefreshRateSeconds : 30;

                        foreach (ComboBoxItem item in PeriodCombo.Items)
            {
                if (item.Tag?.ToString() == _settings.DefaultPeriodDays.ToString())
                {
                    PeriodCombo.SelectedItem = item;
                    break;
                }
            }
            if (PeriodCombo.SelectedIndex == -1) PeriodCombo.SelectedIndex = 1;

            UpdateRefreshIntervalPanel();
            UpdateRefreshIntervalText();
        }

        private void AutoRefreshCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRefreshIntervalPanel();
        }

        private void UpdateRefreshIntervalPanel()
        {
            if (RefreshIntervalPanel == null || RefreshSlider == null || AutoRefreshCheck == null)
                return;

            var isEnabled = AutoRefreshCheck.IsChecked == true;
            RefreshIntervalPanel.Opacity = isEnabled ? 1 : 0.5;
            RefreshSlider.IsEnabled = isEnabled;
        }

        private void RefreshSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateRefreshIntervalText();
        }

        private void UpdateRefreshIntervalText()
        {
            if (RefreshIntervalText == null || RefreshSlider == null) return;

            int value = (int)RefreshSlider.Value;

            if (value >= 60)
            {
                int minutes = value / 60;
                int seconds = value % 60;

                RefreshIntervalText.Text = seconds > 0
                    ? $"{minutes} мин {seconds} сек"
                    : $"{minutes} мин";
            }
            else
            {
                RefreshIntervalText.Text = $"{value} сек";
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Сбросить все настройки до значений по умолчанию?",
                "Сброс настроек",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            ShowKpiCheck.IsChecked = true;
            ShowChartsCheck.IsChecked = true;
            ShowTableCheck.IsChecked = true;
            PeriodCombo.SelectedIndex = 0;             AutoRefreshCheck.IsChecked = true;
            RefreshSlider.Value = 30;

            UpdateRefreshIntervalPanel();
            UpdateRefreshIntervalText();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.ShowKpiBlock = ShowKpiCheck.IsChecked == true;
                _settings.ShowChartsBlock = ShowChartsCheck.IsChecked == true;
                _settings.ShowDetailedTable = ShowTableCheck.IsChecked == true;

                if (PeriodCombo.SelectedItem is ComboBoxItem selected &&
                    int.TryParse(selected.Tag?.ToString(), out int days))
                {
                    _settings.DefaultPeriodDays = days;
                }

                if (AutoRefreshCheck.IsChecked == true)
                {
                    _settings.RefreshRateSeconds = (int)RefreshSlider.Value;
                }
                else
                {
                    _settings.RefreshRateSeconds = 0;
                }

                _context.SaveChanges();

                MessageBox.Show("Настройки сохранены. Переоткройте дашборд, чтобы увидеть изменения.",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}