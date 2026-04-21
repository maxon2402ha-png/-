using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning; // Добавлено для SupportedOSPlatform
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")] // Исправлено: CA1416
    public partial class ManageEmployeesControl : UserControl
    {
        private List<Employee> _employees = new();
        private List<User> _users = new();
        private string _searchText = string.Empty;

        public ManageEmployeesControl()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = new AppDbContext();

                _employees = db.Employees
                    .Include(e => e.User)
                    .AsNoTracking()
                    .OrderBy(e => e.Name)
                    .ToList();

                _users = db.Users
                    .AsNoTracking()
                    .OrderBy(u => u.Username)
                    .ToList();

                ApplyFilter();
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var filtered = _employees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var search = _searchText.ToLower();
                filtered = filtered.Where(e =>
                    e.Name.ToLower().Contains(search) ||
                    (e.User != null && e.User.Username.ToLower().Contains(search)));
            }

            var list = filtered.ToList();
            if (EmployeesGrid != null)
            {
                EmployeesGrid.ItemsSource = list;
            }
        }

        private void LoadUsers()
        {
            if (UsersGrid != null)
            {
                UsersGrid.ItemsSource = _users;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _searchText = textBox.Text;
                ApplyFilter();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox != null) SearchBox.Text = string.Empty;
            LoadData();
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // CA1416: AddEmployeeWindow теперь имеет атрибут SupportedOSPlatform
                var addWindow = new AddEmployeeWindow();
                addWindow.Owner = Window.GetWindow(this);

                if (addWindow.ShowDialog() == true)
                {
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия окна: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statsWindow = new SupportStatisticsWindow();
                statsWindow.Owner = Window.GetWindow(this);
                statsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть статистику: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EmployeesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void EmployeesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EmployeesGrid.SelectedItem is Employee emp && emp.User != null)
            {
                EditUser(emp.User);
            }
        }

        private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UsersGrid.SelectedItem is User user)
            {
                EditUser(user);
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Employee emp)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить сотрудника \"{emp.Name}\"?\n" +
                    "Связанная учетная запись также будет удалена.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbEmp = db.Employees.Include(x => x.User).FirstOrDefault(x => x.Id == emp.Id);

                        if (dbEmp != null)
                        {
                            if (dbEmp.User != null) db.Users.Remove(dbEmp.User);
                            db.Employees.Remove(dbEmp);

                            db.SaveChanges();
                            LoadData();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is User user)
            {
                if (user.Role == "Admin")
                {
                    MessageBox.Show("Удаление администраторов через это меню запрещено.", "Ограничение", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Удалить пользователя \"{user.Username}\"?\nЭто действие необратимо.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbUser = db.Users.Find(user.Id);
                        if (dbUser != null)
                        {
                            db.Users.Remove(dbUser);
                            db.SaveChanges();
                            LoadData();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void EditUser(User user)
        {
            try
            {
                var roles = new[] { "Admin", "Support", "Client" };
                var currentIndex = Array.IndexOf(roles, user.Role);

                var dialog = new Window
                {
                    Title = "Изменение роли",
                    Width = 300,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"))
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = $"Пользователь: {user.Username}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10),
                    Foreground = Brushes.Black
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "Выберите роль:",
                    Margin = new Thickness(0, 0, 0, 5),
                    Foreground = Brushes.Gray
                });

                var comboBox = new ComboBox
                {
                    ItemsSource = roles,
                    SelectedIndex = currentIndex >= 0 ? currentIndex : 2,
                    Height = 32,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                panel.Children.Add(comboBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var cancelButton = new Button
                {
                    Content = "Отмена",
                    Width = 80,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1)
                };
                cancelButton.Click += (s, ev) => dialog.DialogResult = false;

                var saveButton = new Button
                {
                    Content = "Сохранить",
                    Width = 80,
                    Height = 32,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold
                };

                saveButton.Click += (s, ev) =>
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbUser = db.Users.Find(user.Id);
                        if (dbUser != null)
                        {
                            dbUser.Role = comboBox.SelectedItem?.ToString() ?? "Client";
                            db.SaveChanges();
                        }
                        dialog.DialogResult = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                panel.Children.Add(buttonPanel);

                dialog.Content = panel;

                if (dialog.ShowDialog() == true)
                {
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка редактирования: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}