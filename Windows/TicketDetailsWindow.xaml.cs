using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

// Явно указываем, какой Constants использовать
using Constants = КР_Ханников.Core.Constants;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class TicketDetailsWindow : Window
    {
        private readonly int _ticketId;
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly NotificationService _notificationService;
        private readonly TicketService _ticketService;

        private Ticket _ticket = null!;
        private DispatcherTimer? _timer;
        private bool _isInitializing = true;

        public class CommentViewModel
        {
            public string AuthorName { get; set; } = string.Empty;
            public string? AvatarPath { get; set; }
            public string Text { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsMe { get; set; }
            public bool IsInternal { get; set; }
            public string Role { get; set; } = "Client";
        }

        public TicketDetailsWindow(int ticketId) : this(ticketId, new AppDbContext(), null!) { }
        public TicketDetailsWindow(int ticketId, AppDbContext context) : this(ticketId, context, null!) { }
        public TicketDetailsWindow(Ticket ticket, AppDbContext context, AuthService authService)
            : this(ticket.Id, context, authService) { }

        public TicketDetailsWindow(int ticketId, AppDbContext context, AuthService authService)
        {
            InitializeComponent();
            _ticketId = ticketId;
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _authService = authService ?? new AuthService(_context);
            _notificationService = new NotificationService(_context, _authService);
            _ticketService = new TicketService(_context);

            SetupComboBoxes();
            Loaded += Window_Loaded;
        }

        private void SetupComboBoxes()
        {
            // IDE0031: Убраны избыточные проверки на null
            StatusCombo.ItemsSource = Ticket.AllStatuses;
            PriorityCombo.ItemsSource = Enum.GetValues(typeof(TicketPriority));
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTicketDataAsync();
        }

        private async Task LoadTicketDataAsync()
        {
            _isInitializing = true;
            try
            {
                _context.ChangeTracker.Clear();

                var t = await _context.Tickets
                    .Include(x => x.Client)
                    .Include(x => x.Assignee).ThenInclude(a => a.User)
                    .Include(x => x.Solution)
                    .Include(x => x.Feedback)
                    .Include(x => x.Comments).ThenInclude(c => c.Author)
                    .Include(x => x.History)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == _ticketId);

                if (t == null)
                {
                    MessageBox.Show("Тикет не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                _ticket = t;

                // IDE0031: Убраны избыточные проверки на null для UI элементов
                TicketIdHeader.Text = $"#{_ticket.Id}";
                TicketTitleHeader.Text = _ticket.Title;
                DescriptionText.Text = string.IsNullOrWhiteSpace(_ticket.Description) ? "Нет описания" : _ticket.Description;
                StatusText.Text = _ticket.Status;
                StatusBadge.Background = GetStatusBrush(_ticket.Status);

                ClientNameText.Text = _ticket.Client?.Name ?? "Неизвестно";
                ClientEmailText.Text = _ticket.Client?.Email ?? "Нет email";

                var assigneeName = "Не назначен";
                var currentAssignee = _ticket.Assignee;
                if (currentAssignee != null)
                {
                    assigneeName = currentAssignee.User?.Username ?? "Сотрудник";
                }

                AssigneeText.Text = assigneeName;
                CategoryText.Text = _ticket.Category.ToString();
                PriorityText.Text = _ticket.Priority.ToString();

                StatusCombo.SelectedItem = _ticket.Status;
                PriorityCombo.SelectedItem = _ticket.Priority;

                DataContext = _ticket;

                UpdateSlaVisuals();
                SetupWorkTimer();
                CheckAttachment();
                CheckSolution();
                ConfigureAccessAndState();
                LoadComments();
                LoadHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки тикета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void ConfigureAccessAndState()
        {
            var user = _authService.CurrentUser;
            if (user == null) return;

            bool isClient = user.Role == Constants.UserRoles.Client;
            bool isClosed = _ticket.Status == Constants.TicketStatus.Closed;
            bool isSupport = user.Role == Constants.UserRoles.Support || user.Role == Constants.UserRoles.Admin;

            // IDE0031: Убраны избыточные проверки на null
            OperatorControlsPanel.Visibility = (isSupport && !isClosed) ? Visibility.Visible : Visibility.Collapsed;
            InternalCheck.Visibility = isSupport ? Visibility.Visible : Visibility.Collapsed;
            InternalCheck.IsChecked = false;

            if (isClient)
            {
                CloseTicketButton.Visibility = Visibility.Collapsed;
                EditButton.Visibility = Visibility.Collapsed;

                if (isClosed)
                {
                    RateButton.Visibility = Visibility.Visible;
                    if (_ticket.Feedback != null)
                    {
                        RateButton.Content = $"Оценка: {_ticket.Feedback.Rating}/5";
                        RateButton.IsEnabled = false;
                    }
                    else
                    {
                        RateButton.Content = "⭐ Оценить работу";
                        RateButton.IsEnabled = true;
                    }
                }
                else
                {
                    RateButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                RateButton.Visibility = Visibility.Collapsed;
                CloseTicketButton.Visibility = isClosed ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateSlaVisuals()
        {
            // IDE0019: Сопоставление шаблонов для чистоты кода
            if (FindName("SlaStatusText") is not TextBlock slaStatus ||
                FindName("SlaProgressBar") is not ProgressBar slaProgress)
                return;

            if (_ticket.Status == Constants.TicketStatus.Closed || _ticket.Status == Constants.TicketStatus.Resolved)
            {
                slaStatus.Text = "Заявка решена";
                slaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                slaProgress.Value = 100;
                slaProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                return;
            }

            if (!_ticket.DueAt.HasValue)
            {
                slaStatus.Text = "SLA не установлен";
                slaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                slaProgress.Value = 0;
                return;
            }

            var totalTime = _ticket.DueAt.Value - _ticket.CreatedAt;
            var timeRemaining = _ticket.DueAt.Value - DateTime.UtcNow;
            var timePassed = DateTime.UtcNow - _ticket.CreatedAt;

            if (timeRemaining.TotalSeconds <= 0)
            {
                slaStatus.Text = "ПРОСРОЧЕНО";
                slaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                slaProgress.Value = 100;
                slaProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            }
            else
            {
                string remainingText = timeRemaining.TotalHours >= 24
                    ? $"{(int)timeRemaining.TotalDays} дн. {timeRemaining.Hours} ч."
                    : $"{(int)timeRemaining.TotalHours} ч. {timeRemaining.Minutes} мин.";

                slaStatus.Text = remainingText;
                slaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"));

                double percent = (timePassed.TotalSeconds / totalTime.TotalSeconds) * 100;
                slaProgress.Value = Math.Min(percent, 100);

                if (percent > 80) slaProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                else slaProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            }
        }

        private void LoadComments()
        {
            try
            {
                var currentUserId = _authService.CurrentUser?.Id ?? 0;
                var commentsQuery = _ticket.Comments.AsEnumerable();

                if (_authService.CurrentUser?.Role == Constants.UserRoles.Client)
                {
                    commentsQuery = commentsQuery.Where(c => !c.IsInternal);
                }

                var rawComments = commentsQuery.OrderBy(c => c.CreatedAt).ToList();
                var authorIds = rawComments.Select(c => c.UserId).Distinct().ToList();

                var clients = _context.Clients.Where(c => authorIds.Contains(c.UserId)).ToDictionary(c => c.UserId, c => c.Name);
                var employees = _context.Employees.Include(e => e.User).Where(e => authorIds.Contains(e.UserId)).ToList();
                var employeeNames = new Dictionary<int, string>();
                foreach (var emp in employees) employeeNames[emp.UserId] = emp.User?.Username ?? "Сотрудник";

                var viewModels = rawComments.Select(c =>
                {
                    string displayName = c.Author?.Username ?? "Система";

                    // CA1854: Использование TryGetValue
                    if (clients.TryGetValue(c.UserId, out var clientName))
                        displayName = clientName;
                    else if (employeeNames.TryGetValue(c.UserId, out var empName))
                        displayName = empName;

                    return new CommentViewModel
                    {
                        AuthorName = displayName,
                        AvatarPath = c.Author?.AvatarPath,
                        Text = c.Text,
                        CreatedAt = c.CreatedAt.ToLocalTime(),
                        IsMe = c.UserId == currentUserId,
                        IsInternal = c.IsInternal,
                        Role = c.Author?.Role ?? "Client"
                    };
                }).ToList();

                // IDE0031: Убраны избыточные проверки на null
                CommentsList.ItemsSource = viewModels;
                CommentsCountText.Text = viewModels.Count.ToString();

                Dispatcher.InvokeAsync(() => CommentsScrollViewer?.ScrollToBottom(), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading comments: {ex.Message}");
            }
        }

        private void LoadHistory()
        {
            HistoryGrid.ItemsSource = _ticket.History.OrderByDescending(h => h.Timestamp).ToList();
        }

        private async void SendComment_Click(object sender, RoutedEventArgs e)
        {
            var text = CommentBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_authService.CurrentUser == null) return;

            try
            {
                var user = _authService.CurrentUser;
                bool isInternal = false;

                // IDE0031
                if (user.Role != Constants.UserRoles.Client)
                {
                    isInternal = InternalCheck.IsChecked == true;
                }

                using var isolatedDb = App.CreateDbContext();
                var isolatedTicketService = new TicketService(isolatedDb);

                await isolatedTicketService.AddCommentAsync(_ticketId, user.Id, text, isInternal);

                if (user.Role == Constants.UserRoles.Client && _ticket.AssigneeEmployeeId.HasValue)
                {
                    var emp = await isolatedDb.Employees.FindAsync(_ticket.AssigneeEmployeeId.Value);
                    if (emp != null) _notificationService.NotifyOperatorsAboutNewTicket(_ticket);
                }

                CommentBox.Clear();
                InternalCheck.IsChecked = false;

                await LoadTicketDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || StatusCombo.SelectedItem == null) return;

            var newStatus = StatusCombo.SelectedItem.ToString();

            try
            {
                using var db = App.CreateDbContext();
                var ticketService = new TicketService(db);
                await ticketService.ChangeStatusAsync(_ticketId, newStatus!);
                await LoadTicketDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при смене статуса: {ex.Message}");
            }
        }

        private async void PriorityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || PriorityCombo.SelectedItem == null) return;

            var newPriority = (TicketPriority)PriorityCombo.SelectedItem;

            try
            {
                using var db = App.CreateDbContext();
                var ticket = await db.Tickets.FindAsync(_ticketId);

                if (ticket != null && ticket.Priority != newPriority)
                {
                    ticket.Priority = newPriority;

                    db.TicketHistories.Add(new TicketHistory
                    {
                        TicketId = _ticketId,
                        Action = "Изменение приоритета",
                        Details = $"Приоритет изменен на {newPriority}",
                        Timestamp = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                    await LoadTicketDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при смене приоритета: {ex.Message}");
            }
        }

        private void SetupWorkTimer()
        {
            _timer?.Stop();

            if (_ticket.Status != Constants.TicketStatus.InProgress && _ticket.Status != Constants.TicketStatus.Open)
            {
                TimerText.Text = "Остановлен";
                TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                return;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.UtcNow - _ticket.CreatedAt.ToUniversalTime();
                TimerText.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
            };

            TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8"));
            TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
            _timer.Start();
        }

        private async void CloseTicket_Click(object sender, RoutedEventArgs e)
        {
            string resolutionText = Interaction.InputBox(
                "Введите финальное решение по заявке для клиента:",
                "Решение заявки",
                "Вопрос решен штатным образом.");

            if (string.IsNullOrWhiteSpace(resolutionText))
                return;

            try
            {
                using var db = App.CreateDbContext();
                var t = await db.Tickets.FindAsync(_ticketId);
                if (t != null)
                {
                    var oldStatus = t.Status;
                    t.Status = Constants.TicketStatus.Closed;
                    t.ClosedAt = DateTime.UtcNow;

                    var sol = new Solution
                    {
                        TicketId = _ticketId,
                        ResolutionText = resolutionText,
                        ResolutionDate = DateTime.UtcNow
                    };
                    db.Solutions.Add(sol);

                    db.TicketHistories.Add(new TicketHistory
                    {
                        TicketId = _ticketId,
                        Action = "Закрытие",
                        Details = $"Статус: {oldStatus} -> Closed",
                        Timestamp = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                    _notificationService.NotifyStatusChanged(t, oldStatus, Constants.TicketStatus.Closed);

                    await LoadTicketDataAsync();
                    MessageBox.Show("Тикет успешно решён", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при закрытии: {ex.Message}");
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var editWin = new EditTicketWindow(_ticketId, _context) { Owner = this };
            if (editWin.ShowDialog() == true)
            {
                await LoadTicketDataAsync();
            }
        }

        private async void Rate_Click(object sender, RoutedEventArgs e)
        {
            string ratingStr = Interaction.InputBox("Оцените качество работы специалиста (от 1 до 5):", "Оценка качества", "5");

            if (string.IsNullOrWhiteSpace(ratingStr))
                return;

            if (!int.TryParse(ratingStr, out int rating) || rating < 1 || rating > 5)
            {
                MessageBox.Show("Оценка должна быть числом от 1 до 5.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string comment = Interaction.InputBox("Ваш комментарий (по желанию):", "Отзыв", "");

            try
            {
                using var db = App.CreateDbContext();
                var feedback = new Feedback
                {
                    TicketId = _ticketId,
                    ClientId = _ticket.ClientId,
                    SupportId = _ticket.AssigneeEmployeeId,
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.UtcNow
                };

                db.Feedbacks.Add(feedback);
                await db.SaveChangesAsync();

                MessageBox.Show("Спасибо за ваш отзыв!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadTicketDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void CheckAttachment()
        {
            bool hasFile = !string.IsNullOrEmpty(_ticket.AttachmentPath);
            AttachmentPanel.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
            if (hasFile)
            {
                FileNameText.Text = _ticket.AttachmentFileName ?? "Файл";
                AttachmentPanel.Tag = _ticket.AttachmentPath;
            }
        }

        private void CheckSolution()
        {
            bool hasSol = _ticket.Solution != null;
            SolutionPanel.Visibility = hasSol ? Visibility.Visible : Visibility.Collapsed;
            if (hasSol)
            {
                SolutionText.Text = _ticket.Solution!.ResolutionText;
            }
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AttachmentPanel.Tag is string filePath && System.IO.File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка");
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        // CA1822: Сделали метод статическим
        private static SolidColorBrush GetStatusBrush(string status)
        {
            return status switch
            {
                Constants.TicketStatus.Open => new SolidColorBrush(Colors.Orange),
                Constants.TicketStatus.InProgress => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Constants.TicketStatus.Resolved => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Constants.TicketStatus.Closed => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}