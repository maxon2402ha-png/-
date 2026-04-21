using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Runtime.Versioning;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Helpers;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class UserProfileWindow : Window
    {
        private readonly int _userId;
        // Исправлено: Инициализируем через null-forgiving operator, так как данные загружаются в конструкторе
        private User _currentUser = null!;
        private string _tempAvatarPath = string.Empty;

        public UserProfileWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserData();
        }

        private void LoadUserData()
        {
            try
            {
                using var db = new AppDbContext();

                var user = db.Users.FirstOrDefault(u => u.Id == _userId);

                if (user == null)
                {
                    MessageBox.Show("Пользователь не найден", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                _currentUser = user;

                var employee = db.Employees.FirstOrDefault(e => e.UserId == _userId);
                var client = db.Clients.FirstOrDefault(c => c.UserId == _userId);

                var displayName = employee?.Name ?? client?.Name ?? _currentUser.Username;
                if (DisplayNameText != null) DisplayNameText.Text = displayName;
                if (UsernameText != null) UsernameText.Text = $"@{_currentUser.Username}";
                if (UsernameBox != null) UsernameBox.Text = _currentUser.Username;

                if (RoleText != null)
                {
                    RoleText.Text = _currentUser.Role switch
                    {
                        "Admin" => "Администратор",
                        "Support" => "Поддержка",
                        "Client" => "Клиент",
                        _ => _currentUser.Role
                    };
                }

                if (CreatedAtText != null) CreatedAtText.Text = _currentUser.CreatedAt.ToString("dd.MM.yyyy");
                if (LastLoginText != null) LastLoginText.Text = _currentUser.LastLoginAt?.ToString("dd.MM.yyyy HH:mm") ?? "—";

                UpdateAvatarDisplay(_currentUser.AvatarPath);

                var settings = db.UserUiSettings.FirstOrDefault(s => s.UserId == _userId);

                if (settings == null)
                {
                    settings = new UserUiSettings { UserId = _userId, Theme = "Light" };
                    db.UserUiSettings.Add(settings);
                    db.SaveChanges();
                }

                if (ThemeToggle != null)
                {
                    ThemeToggle.IsChecked = settings.Theme == "Dark";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки профиля: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isDark = ThemeToggle.IsChecked == true;
                ThemeManager.ApplyTheme(isDark);

                using var db = new AppDbContext();
                var settings = db.UserUiSettings.FirstOrDefault(s => s.UserId == _userId);
                if (settings == null)
                {
                    settings = new UserUiSettings { UserId = _userId };
                    db.UserUiSettings.Add(settings);
                }

                settings.Theme = isDark ? "Dark" : "Light";
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения темы: {ex.Message}");
            }
        }

        private void UpdateAvatarDisplay(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (AvatarBrush != null) AvatarBrush.ImageSource = null;
                if (DefaultAvatarIcon != null) DefaultAvatarIcon.Visibility = Visibility.Visible;
            }
            else
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();

                    if (AvatarBrush != null) AvatarBrush.ImageSource = bitmap;
                    if (DefaultAvatarIcon != null) DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    if (AvatarBrush != null) AvatarBrush.ImageSource = null;
                    if (DefaultAvatarIcon != null) DefaultAvatarIcon.Visibility = Visibility.Visible;
                }
            }
        }

        private void ChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Выберите аватар"
            };

            if (dialog.ShowDialog() == true)
            {
                _tempAvatarPath = dialog.FileName;
                UpdateAvatarDisplay(_tempAvatarPath);
            }
        }

        private void NewPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
            CheckPasswordMatch();
            UpdateChangePasswordButton();
        }

        private void ConfirmPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            CheckPasswordMatch();
            UpdateChangePasswordButton();
        }

        private void UpdatePasswordStrength()
        {
            if (NewPassBox == null) return;

            var password = NewPassBox.Password;
            int strength = 0;

            if (password.Length >= 6) strength++;
            if (password.Length >= 10) strength++;
            if (Regex.IsMatch(password, @"[A-Z]") && Regex.IsMatch(password, @"[a-z]")) strength++;
            if (Regex.IsMatch(password, @"\d")) strength++;

            var bars = new[] { StrengthBar1, StrengthBar2, StrengthBar3, StrengthBar4 };
            var colors = new[] { "#EF4444", "#F59E0B", "#10B981", "#10B981" };

            for (int i = 0; i < 4; i++)
            {
                if (bars[i] != null)
                {
                    bars[i].Background = i < strength
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[Math.Min(strength - 1, 2)]))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                }
            }
        }

        private void CheckPasswordMatch()
        {
            if (ConfirmPassBox == null || PasswordMatchPanel == null) return;

            if (ConfirmPassBox.Password.Length == 0)
            {
                PasswordMatchPanel.Visibility = Visibility.Collapsed;
                return;
            }

            PasswordMatchPanel.Visibility = Visibility.Visible;

            bool isMatch = NewPassBox?.Password == ConfirmPassBox.Password;
            string color = isMatch ? "#10B981" : "#EF4444";

            if (PasswordMatchIcon != null)
                PasswordMatchIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

            if (PasswordMatchText != null)
            {
                PasswordMatchText.Text = isMatch ? "Пароли совпадают" : "Пароли не совпадают";
                PasswordMatchText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
        }

        private void UpdateChangePasswordButton()
        {
            if (ChangePasswordButton == null || OldPassBox == null || NewPassBox == null || ConfirmPassBox == null) return;

            ChangePasswordButton.IsEnabled =
                OldPassBox.Password.Length > 0 &&
                NewPassBox.Password.Length >= 6 &&
                NewPassBox.Password == ConfirmPassBox.Password;
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (OldPassBox == null || string.IsNullOrEmpty(OldPassBox.Password))
            {
                MessageBox.Show("Введите текущий пароль");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                // Проверка на null перед обращением к свойствам
                if (_currentUser == null) return;

                var user = db.Users.Find(_userId);
                if (user == null) return;

                if (!CryptoHelper.VerifyPassword(OldPassBox.Password, user.PasswordHash))
                {
                    MessageBox.Show("Неверный текущий пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                user.PasswordHash = CryptoHelper.HashPassword(NewPassBox.Password);
                db.SaveChanges();

                MessageBox.Show("Пароль успешно изменён");
                OldPassBox.Clear();
                NewPassBox.Clear();
                ConfirmPassBox?.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();

                if (!string.IsNullOrEmpty(_tempAvatarPath))
                {
                    var avatarsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "КР_Ханников", "Avatars");
                    if (!Directory.Exists(avatarsDir)) Directory.CreateDirectory(avatarsDir);

                    var newFileName = $"avatar_{_userId}_{DateTime.Now.Ticks}{Path.GetExtension(_tempAvatarPath)}";
                    var destPath = Path.Combine(avatarsDir, newFileName);

                    File.Copy(_tempAvatarPath, destPath, true);

                    var userToUpdate = db.Users.Find(_userId);
                    if (userToUpdate != null)
                    {
                        userToUpdate.AvatarPath = destPath;
                        db.SaveChanges();
                    }
                }

                if (UsernameBox != null && !string.IsNullOrWhiteSpace(UsernameBox.Text))
                {
                    var newUsername = UsernameBox.Text.Trim();
                    if (db.Users.Any(u => u.Username == newUsername && u.Id != _userId))
                    {
                        MessageBox.Show("Такой логин уже занят", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var user = db.Users.Find(_userId);
                if (user == null)
                {
                    MessageBox.Show("Пользователь не найден", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (UsernameBox != null)
                {
                    user.Username = UsernameBox.Text.Trim();
                }

                db.SaveChanges();

                db.AuditLogs.Add(new AuditLog
                {
                    Username = user.Username,
                    Action = "Обновление профиля",
                    Details = "Данные профиля обновлены",
                    Timestamp = DateTime.UtcNow
                });
                db.SaveChanges();

                MessageBox.Show("Изменения сохранены", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}