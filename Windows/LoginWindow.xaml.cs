using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Helpers;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class LoginWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private static bool _mlInitStarted = false;

        public LoginWindow()
        {
            InitializeComponent();
            _context = App.CreateDbContext();
            _authService = new AuthService(_context);

            LoadSavedCredentials();
        }

        public LoginWindow(AppDbContext context, AuthService authService)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            LoadSavedCredentials();
        }

        private void LoadSavedCredentials()
        {
            try
            {
                // Используем сохраненные настройки
                if (Properties.Settings.Default.IsRemembered)
                {
                    UsernameBox.Text = Properties.Settings.Default.Username;
                    RememberMeCheck.IsChecked = true;
                    Loaded += (s, e) => PasswordBox.Focus();
                }
                else
                {
                    Loaded += (s, e) => UsernameBox.Focus();
                }
            }
            catch
            {
                // Если настройки недоступны, просто игнорируем
            }
        }

        private void SaveCredentials(string username)
        {
            try
            {
                if (RememberMeCheck.IsChecked == true)
                {
                    Properties.Settings.Default.Username = username;
                    Properties.Settings.Default.IsRemembered = true;
                }
                else
                {
                    Properties.Settings.Default.Username = string.Empty;
                    Properties.Settings.Default.IsRemembered = false;
                }
                Properties.Settings.Default.Save();
            }
            catch { /* Игнорируем ошибки сохранения настроек */ }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TryBeginAnimation("WindowLoadAnimation", this);
            TryBeginAnimation("BrandPanelAnimation", FindName("BrandPanel") as FrameworkElement);
            TryBeginAnimation("LoginCardAnimation", FindName("LoginContainer") as FrameworkElement);
        }

        private void TryBeginAnimation(string resourceKey, FrameworkElement? target = null)
        {
            try
            {
                if (this.Resources.Contains(resourceKey))
                {
                    var anim = this.FindResource(resourceKey) as Storyboard;
                    if (anim != null)
                    {
                        if (target != null) anim.Begin(target);
                        else anim.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoginWindow] Animation error: {ex.Message}");
            }
        }

        private void StartMlModelTrainingIfAdmin()
        {
            try
            {
                if (_mlInitStarted) return;
                var currentUser = _authService.CurrentUser;
                if (currentUser == null || currentUser.Role != Constants.UserRoles.Admin) return;

                _mlInitStarted = true;
                Task.Run(() =>
                {
                    try
                    {
                        using var trainingContext = App.CreateDbContext();
                        TicketModelTrainer.TrainAndSaveModels(trainingContext);
                    }
                    catch (Exception innerEx) { Debug.WriteLine($"[ML] Error: {innerEx.Message}"); }
                });
            }
            catch { _mlInitStarted = false; }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HideError();
                var username = UsernameBox.Text;
                var password = PasswordBox.Password;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    ShowError("Пожалуйста, заполните все поля");
                    return;
                }

                if (_authService.Login(username, password))
                {
                    SaveCredentials(username);
                    if (_authService.CurrentUser != null) ApplyUserTheme(_authService.CurrentUser.Id);
                    StartMlModelTrainingIfAdmin();

                    var mainWindow = new MainWindow(_context, _authService);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ShowError("Неверный логин или пароль");
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            if (ErrorText != null) ErrorText.Text = message;
            if (ErrorPanel != null) ErrorPanel.Visibility = Visibility.Visible;
            if (ErrorBorder != null) ErrorBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            if (ErrorPanel != null) ErrorPanel.Visibility = Visibility.Collapsed;
            if (ErrorBorder != null) ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void ApplyUserTheme(int userId)
        {
            try
            {
                using var db = App.CreateDbContext();
                var settings = db.UserUiSettings.FirstOrDefault(s => s.UserId == userId);
                bool isDark = settings != null && settings.Theme == "Dark";
                ThemeManager.ApplyTheme(isDark);
            }
            catch { }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ИСПРАВЛЕНО: RegistrationWindow вместо RegisterWindow
                var regWindow = new RegistrationWindow(_context)
                {
                    Owner = this
                };

                if (regWindow.ShowDialog() == true)
                {
                    MessageBox.Show("Регистрация успешна! Войдите в систему.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия регистрации: {ex.Message}");
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Login_Click(sender, e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}