using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
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
        private readonly AuthService _authService;
        private string? _attachedFilePath;

        public CreateTicketWindow(AuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

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
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    MessageBox.Show("Файл слишком большой. Максимальный размер 10 МБ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Создание тикетов доступно только для клиентов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TitleTextBox.Text) || string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                {
                    MessageBox.Show("Пожалуйста, заполните Тему и Описание проблемы.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Анализ ИИ и Создание...";
                Cursor = Cursors.Wait;

                DateTime? due = null;
                if (DueDatePicker.SelectedDate.HasValue)
                {
                    var date = DueDatePicker.SelectedDate.Value;
                    var timeText = string.IsNullOrWhiteSpace(DueTimeBox.Text) ? "18:00" : DueTimeBox.Text.Trim();
                    if (TimeSpan.TryParse(timeText, out var ts))
                    {
                        due = date.Date.Add(ts).ToUniversalTime();
                    }
                }

                using var db = App.CreateDbContext();
                var ticketService = new TicketService(db);
                var notificationService = new NotificationService(db, _authService);

                var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

                // 1. СОЗДАНИЕ ТИКЕТА. 
                // Мы НЕ передаем категорию и приоритет. TicketService вызовет ML-модель сам!
                var newTicket = await ticketService.CreateAsync(
                    clientId: client!.Id,
                    title: TitleTextBox.Text.Trim(),
                    description: DescriptionTextBox.Text.Trim(),
                    manualDueAt: due,
                    authorUserId: currentUser.Id
                );

                // 2. Сохранение файла
                if (!string.IsNullOrEmpty(_attachedFilePath) && File.Exists(_attachedFilePath))
                {
                    try
                    {
                        var attachmentsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "КР_Ханников", "Attachments");
                        if (!Directory.Exists(attachmentsDir)) Directory.CreateDirectory(attachmentsDir);

                        var extension = Path.GetExtension(_attachedFilePath);
                        var fileName = $"{newTicket.Id}_{Guid.NewGuid()}{extension}";
                        var destPath = Path.Combine(attachmentsDir, fileName);

                        using (var sourceStream = new FileStream(_attachedFilePath, FileMode.Open, FileAccess.Read))
                        using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        newTicket.AttachmentPath = destPath;
                        newTicket.AttachmentFileName = Path.GetFileName(_attachedFilePath);
                        await db.SaveChangesAsync();
                    }
                    catch { }
                }

                notificationService.NotifyOperatorsAboutNewTicket(newTicket);

                // Показываем клиенту, что ИИ всё сделал за него
                MessageBox.Show($"Тикет #{newTicket.Id} успешно создан!\n\n✨ Нейросеть обработала заявку:\nКатегория: {newTicket.Category}\nПриоритет: {newTicket.Priority}",
                                "Успешно создано", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}", "Сбой", MessageBoxButton.OK, MessageBoxImage.Error);
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