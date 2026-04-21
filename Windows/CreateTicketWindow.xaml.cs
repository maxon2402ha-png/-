using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class CreateTicketWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly NotificationService _notificationService;
        private readonly TicketService _ticketService;

        private string? _attachedFilePath;

        // Конструктор по умолчанию (для дизайнера)
        public CreateTicketWindow() : this(new AppDbContext(), new AuthService(new AppDbContext())) { }

        public CreateTicketWindow(AppDbContext context, AuthService authService)
        {
            InitializeComponent();

            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _notificationService = new NotificationService(_context, _authService);
            _ticketService = new TicketService(_context);

            Debug.WriteLine("[CreateTicketWindow] Инициализация завершена.");
            Loaded += (s, e) => TitleTextBox.Focus();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Файлы|*.pdf;*.docx;*.xlsx;*.txt;*.jpg;*.png",
                Title = "Выберите файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var fileInfo = new FileInfo(openFileDialog.FileName);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10 MB
                {
                    MessageBox.Show("Файл слишком большой. Максимальный размер 10 МБ.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _attachedFilePath = openFileDialog.FileName;
                FileNameTextBlock.Text = Path.GetFileName(_attachedFilePath);
                FileNameTextBlock.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentUser = _authService.CurrentUser;

                if (currentUser == null || currentUser.Role != Constants.UserRoles.Client)
                {
                    MessageBox.Show("Создание тикетов доступно только для клиентов!",
                                    "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
                if (client == null)
                {
                    MessageBox.Show("Ошибка: профиль клиента не найден в базе данных!",
                                    "Ошибка целостности", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TitleTextBox.Text) || string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                {
                    MessageBox.Show("Пожалуйста, заполните Тему и Описание проблемы.",
                                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Блокировка UI
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Создание...";
                Cursor = Cursors.Wait;

                DateTime? due = null;
                if (DueDatePicker.SelectedDate.HasValue)
                {
                    var date = DueDatePicker.SelectedDate.Value;
                    var timeText = string.IsNullOrWhiteSpace(DueTimeBox.Text) ? "18:00" : DueTimeBox.Text.Trim();
                    if (TimeSpan.TryParse(timeText, out var ts))
                        due = date.Date.Add(ts);
                }

                // 1. Создание тикета через сервис (авто-назначение, классификация)
                var newTicket = await _ticketService.CreateAsync(
                    clientId: client.Id,
                    title: TitleTextBox.Text.Trim(),
                    description: DescriptionTextBox.Text.Trim(),
                    manualDueAt: due,
                    authorUserId: currentUser.Id
                );

                // 2. Обработка вложения (если есть)
                bool updateNeeded = false;

                if (!string.IsNullOrEmpty(_attachedFilePath) && File.Exists(_attachedFilePath))
                {
                    try
                    {
                        var attachmentsDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "КР_Ханников", "Attachments");

                        if (!Directory.Exists(attachmentsDir))
                            Directory.CreateDirectory(attachmentsDir);

                        var extension = Path.GetExtension(_attachedFilePath);
                        var fileName = $"{newTicket.Id}_{Guid.NewGuid()}{extension}";
                        var destPath = Path.Combine(attachmentsDir, fileName);

                        // Асинхронное копирование
                        using (var sourceStream = new FileStream(_attachedFilePath, FileMode.Open, FileAccess.Read))
                        using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        newTicket.AttachmentPath = destPath;
                        newTicket.AttachmentFileName = Path.GetFileName(_attachedFilePath);
                        updateNeeded = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Тикет создан, но не удалось сохранить файл: {ex.Message}",
                                        "Ошибка вложения", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // Сохраняем путь к файлу, если он был добавлен
                if (updateNeeded)
                {
                    await _context.SaveChangesAsync();
                }

                // 3. Отправка уведомления
                try
                {
                    _notificationService.NotifyOperatorsAboutNewTicket(newTicket);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка отправки уведомления: {ex.Message}");
                }

                MessageBox.Show($"Тикет #{newTicket.Id} успешно создан!\n\n" +
                                $"Система определила:\n" +
                                $"Категория: {newTicket.Category}\n" +
                                $"Приоритет: {newTicket.Priority}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException != null
                    ? dbEx.InnerException.Message
                    : dbEx.Message;

                MessageBox.Show($"ОШИБКА БАЗЫ ДАННЫХ:\n{innerMessage}",
                                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла неожиданная ошибка:\n{ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (SubmitButton != null)
                {
                    SubmitButton.IsEnabled = true;
                    SubmitButton.Content = "Создать тикет";
                }
                Cursor = Cursors.Arrow;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}