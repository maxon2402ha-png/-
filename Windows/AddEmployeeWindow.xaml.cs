using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class AddEmployeeWindow : Window
    {
        public AddEmployeeWindow()
        {
            InitializeComponent();
            NameBox.Focus();
        }

        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
                        if (UsernameBox.Text.Contains(" "))
            {
                int caretIndex = UsernameBox.CaretIndex;
                UsernameBox.Text = UsernameBox.Text.Replace(" ", "");
                UsernameBox.CaretIndex = caretIndex > 0 ? caretIndex - 1 : 0;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;
                        var role = (RoleBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? Constants.UserRoles.Support;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля (Имя, Логин, Пароль).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("В целях безопасности пароль должен содержать не менее 6 символов.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = App.CreateDbContext();

                                if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует в системе! Пожалуйста, придумайте другой логин.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    UsernameBox.Focus();
                    return;
                }

                                var user = new User
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13),
                    Role = role,
                    IsEmailVerified = true,                     CreatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync(); 
                                var employee = new Employee
                {
                    UserId = user.Id,
                    Name = name,
                    Role = role,
                    MaxActiveTickets = 5                 };

                db.Employees.Add(employee);

                                db.AuditLogs.Add(new AuditLog
                {
                    Username = "System",
                    Action = "Создание сотрудника",
                    Details = $"Создан новый оператор '{name}' с логином '{username}' ({role})",
                    Timestamp = DateTime.UtcNow
                });

                await db.SaveChangesAsync();

                MessageBox.Show($"Сотрудник {name} успешно добавлен в систему и может приступить к работе!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении в базу данных:\n{ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}