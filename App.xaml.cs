using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
                        var culture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            try
            {
                                using (var context = CreateDbContext())
                {
                                        context.Database.EnsureCreated();

                                        EnsureAdminExists(context);

                                        DbSeeder.SeedAsync(context).Wait();
                }

                                Task.Run(() =>
                {
                    try
                    {
                        using var mlContext = CreateDbContext();
                        var classifier = new MlTicketClassifier();
                        classifier.TrainModels(mlContext);
                        Debug.WriteLine("[ML] Модель успешно обучена на исторических данных!");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ML Error] Ошибка обучения: {ex.Message}");
                    }
                });

                                var loginContext = CreateDbContext();
                var authService = new AuthService(loginContext);
                var loginWindow = new LoginWindow(loginContext, authService);
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска:\n{ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void EnsureAdminExists(AppDbContext context)
        {
            try
            {
                string adminUsername = "admin";
                string defaultPassword = "admin123";

                var adminUser = context.Users.FirstOrDefault(u => u.Username == adminUsername);

                if (adminUser != null)
                {
                                        if (!BCrypt.Net.BCrypt.EnhancedVerify(defaultPassword, adminUser.PasswordHash))
                    {
                        adminUser.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(defaultPassword, 13);
                    }

                    adminUser.Role = Constants.UserRoles.Admin;

                    var emp = context.Employees.FirstOrDefault(e => e.UserId == adminUser.Id);
                    if (emp == null)
                    {
                        context.Employees.Add(new Employee
                        {
                            Name = "Главный Администратор",
                            UserId = adminUser.Id,
                            Role = Constants.UserRoles.Admin,
                            MaxActiveTickets = 999
                        });
                    }
                    else
                    {
                        emp.Role = Constants.UserRoles.Admin;
                    }
                }
                else
                {
                    var newAdmin = new User
                    {
                        Username = adminUsername,
                        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(defaultPassword, 13),
                        Role = Constants.UserRoles.Admin,
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Users.Add(newAdmin);
                    context.SaveChanges();

                    context.Employees.Add(new Employee
                    {
                        Name = "Главный Администратор",
                        UserId = newAdmin.Id,
                        Role = Constants.UserRoles.Admin,
                        MaxActiveTickets = 999
                    });
                }

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Admin Check Error] {ex.Message}");
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"В приложении произошла ошибка:\n{e.Exception.Message}",
                "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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