using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;
using КР_Ханников.Windows;

namespace КР_Ханников
{
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        public App()
        {
            // Лечение ошибок даты в Postgres
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var culture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            try
            {
                using (var context = CreateDbContext())
                {
                    // 1. Применяем миграции
                    context.Database.Migrate();

                    // 2. Обновляем или создаем админа
                    EnsureAdminExists(context);
                }

                // 3. Запускаем окно входа
                var loginContext = CreateDbContext();
                var authService = new Services.AuthService(loginContext);
                var loginWindow = new LoginWindow(loginContext, authService);
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка запуска:\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void EnsureAdminExists(AppDbContext context)
        {
            try
            {
                // Ищем пользователя с логином 'admin'
                var adminUser = context.Users.FirstOrDefault(u => u.Username == "admin");

                // Хеш пароля "admin123"
                // Используем стандартный хеш, чтобы подходил под любую реализацию проверки
                string newHash = BCrypt.Net.BCrypt.HashPassword("admin123");

                if (adminUser != null)
                {
                    // === СЦЕНАРИЙ ОБНОВЛЕНИЯ (если админ уже был) ===
                    Debug.WriteLine("Админ найден. Сброс пароля...");
                    adminUser.PasswordHash = newHash;
                    adminUser.Role = "Admin"; // На всякий случай возвращаем права

                    // Проверяем, есть ли запись сотрудника
                    var emp = context.Employees.FirstOrDefault(e => e.UserId == adminUser.Id);
                    if (emp == null)
                    {
                        context.Employees.Add(new Employee
                        {
                            Name = "Administrator",
                            UserId = adminUser.Id,
                            Role = "Admin",
                            MaxActiveTickets = 999
                        });
                    }
                }
                else
                {
                    // === СЦЕНАРИЙ СОЗДАНИЯ (если админа не было) ===
                    Debug.WriteLine("Админ не найден. Создание нового...");
                    var newAdmin = new User
                    {
                        Username = "admin",
                        PasswordHash = newHash,
                        Role = "Admin",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.Now
                    };
                    context.Users.Add(newAdmin);
                    context.SaveChanges(); 

                    context.Employees.Add(new Employee
                    {
                        Name = "Administrator",
                        UserId = newAdmin.Id,
                        Role = "Admin",
                        MaxActiveTickets = 999
                    });
                }

                context.SaveChanges();
                // MessageBox.Show("Администратор готов!\nЛогин: admin\nПароль: admin123", "Инфо");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка настройки админа: {ex.Message}");
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Ошибка в приложении:\n{e.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        public static AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(Constants.Database.GetConnectionString());
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}