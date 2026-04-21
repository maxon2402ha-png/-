using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

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

        // ViewModel для комментария
        public class CommentViewModel
        {
            public string AuthorName { get; set; } = string.Empty;
            public string? AvatarPath { get; set; }
            public string Text { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsMe { get; set; }
            public bool IsInternal { get; set; }
            public string Role { get; set; } = "Client"; // Добавлено поле Роль для цвета аватарки
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

            LoadTicket();
            StartTimer();
        }

        private void LoadTicket()
        {
            try
            {
                _context.ChangeTracker.Clear();

                var t = _context.Tickets
                    .Include(x => x.Client)
                    .Include(x => x.Assignee).ThenInclude(a => a != null ? a.User : null)
                    .Include(x => x.Solution)
                    .Include(x => x.Feedback)
                    .FirstOrDefault(x => x.Id == _ticketId);

                if (t == null)
                {
                    MessageBox.Show("Тикет не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }
                _ticket = t;

                if (TicketIdHeader != null) TicketIdHeader.Text = $"#{_ticket.Id}";
                if (TicketTitleHeader != null) TicketTitleHeader.Text = _ticket.Title;
                if (DescriptionText != null) DescriptionText.Text = _ticket.Description;
                if (StatusText != null) StatusText.Text = _ticket.Status;

                if (StatusBadge != null)
                    StatusBadge.Background = GetStatusBrush(_ticket.Status);

                if (ClientNameText != null) ClientNameText.Text = _ticket.Client?.Name ?? "Неизвестно";
                if (ClientEmailText != null) ClientEmailText.Text = _ticket.Client?.Email ?? "Нет email";

                var assigneeName = "Не назначен";
                if (_ticket.Assignee != null)
                    assigneeName = _ticket.Assignee.User?.Username ?? "Сотрудник"; // Можно заменить на Name из Employee если добавить поле

                if (AssigneeText != null) AssigneeText.Text = assigneeName;

                if (CategoryText != null) CategoryText.Text = _ticket.Category.ToString();
                if (PriorityText != null) PriorityText.Text = _ticket.Priority.ToString();
                if (DeadlineText != null)
                    DeadlineText.Text = _ticket.DueAt.HasValue
                        ? _ticket.DueAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                        : "Не задан";

                DataContext = null;
                DataContext = _ticket;

                LoadComments();
                LoadHistory();
                CheckAttachment();
                CheckSolution();
                ConfigureAccessAndState();
                UpdateTimerDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки тикета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigureAccessAndState()
        {
            var user = _authService.CurrentUser;
            if (user == null) return;

            bool isClient = user.Role == Constants.UserRoles.Client;
            bool isClosed = _ticket.Status == Constants.TicketStatus.Closed;
            bool isSupport = user.Role == Constants.UserRoles.Support || user.Role == Constants.UserRoles.Admin;

            if (LinkExternalButton != null) LinkExternalButton.Visibility = isSupport ? Visibility.Visible : Visibility.Collapsed;
            if (SyncExternalButton != null) SyncExternalButton.Visibility = isSupport ? Visibility.Visible : Visibility.Collapsed;

            if (InternalCheck != null)
            {
                InternalCheck.Visibility = isSupport ? Visibility.Visible : Visibility.Collapsed;
                InternalCheck.IsChecked = false;
            }

            if (isClient)
            {
                if (CloseTicketButton != null) CloseTicketButton.Visibility = Visibility.Collapsed;
                if (EditButton != null) EditButton.Visibility = Visibility.Collapsed;

                if (RateButton != null)
                {
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
            }
            else
            {
                if (RateButton != null) RateButton.Visibility = Visibility.Collapsed;
                if (CloseTicketButton != null)
                {
                    CloseTicketButton.IsEnabled = !isClosed;
                    CloseTicketButton.Content = isClosed ? "Тикет закрыт" : "✓ Решить тикет";
                }
            }
        }

        private void LoadComments()
        {
            try
            {
                var currentUserId = _authService.CurrentUser?.Id ?? 0;

                var query = _context.TicketComments
                    .Include(c => c.Author)
                    .Where(c => c.TicketId == _ticketId);

                if (_authService.CurrentUser?.Role == Constants.UserRoles.Client)
                {
                    query = query.Where(c => !c.IsInternal);
                }

                var rawComments = query.OrderBy(c => c.CreatedAt).ToList();

                // Сбор ID для подгрузки имен
                var authorIds = rawComments.Select(c => c.UserId).Distinct().ToList();

                var clients = _context.Clients.Where(c => authorIds.Contains(c.UserId))
                    .ToDictionary(c => c.UserId, c => c.Name);

                // Загружаем имена сотрудников (здесь используем Username, так как в Employee пока нет Name, но можно расширить)
                var employees = _context.Employees.Include(e => e.User)
                    .Where(e => authorIds.Contains(e.UserId))
                    .ToList();

                var employeeNames = new Dictionary<int, string>();
                foreach (var emp in employees)
                    employeeNames[emp.UserId] = emp.User?.Username ?? "Сотрудник";

                var viewModels = rawComments.Select(c =>
                {
                    string displayName = c.Author.Username;

                    if (clients.ContainsKey(c.UserId))
                        displayName = clients[c.UserId];
                    else if (employeeNames.ContainsKey(c.UserId))
                        displayName = employeeNames[c.UserId];

                    return new CommentViewModel
                    {
                        AuthorName = displayName,
                        AvatarPath = c.Author.AvatarPath,
                        Text = c.Text,
                        CreatedAt = c.CreatedAt.ToLocalTime(),
                        IsMe = c.UserId == currentUserId,
                        IsInternal = c.IsInternal,
                        Role = c.Author.Role // Передаем роль для раскраски
                    };
                }).ToList();

                if (CommentsList != null) CommentsList.ItemsSource = viewModels;
                if (CommentsScrollViewer != null) CommentsScrollViewer.ScrollToBottom();
                if (CommentsCountText != null) CommentsCountText.Text = viewModels.Count.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading comments: {ex.Message}");
            }
        }

        private void LoadHistory()
        {
            if (HistoryGrid != null)
                HistoryGrid.ItemsSource = _context.TicketHistories
                    .AsNoTracking()
                    .Where(h => h.TicketId == _ticketId)
                    .OrderByDescending(h => h.Timestamp)
                    .ToList();
        }

        private void SendComment_Click(object sender, RoutedEventArgs e)
        {
            var text = CommentBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_authService.CurrentUser == null) return;

            try
            {
                var user = _authService.CurrentUser;
                bool isInternal = false;

                if (user.Role != Constants.UserRoles.Client && InternalCheck != null)
                    isInternal = InternalCheck.IsChecked == true;

                var c = new TicketComment
                {
                    TicketId = _ticketId,
                    UserId = user.Id,
                    Text = text,
                    IsInternal = isInternal,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TicketComments.Add(c);

                string authorName = user.Username;
                var client = _context.Clients.FirstOrDefault(cl => cl.UserId == user.Id);
                if (client != null) authorName = client.Name;

                string details = isInternal
                    ? $"[Внутренний] {authorName}: {text.Substring(0, Math.Min(text.Length, 30))}..."
                    : $"{authorName}: {text.Substring(0, Math.Min(text.Length, 30))}...";

                _context.TicketHistories.Add(new TicketHistory
                {
                    TicketId = _ticketId,
                    Action = "Комментарий",
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });

                _context.SaveChanges();

                if (user.Role == Constants.UserRoles.Client && _ticket.AssigneeEmployeeId.HasValue)
                {
                    var emp = _context.Employees.Find(_ticket.AssigneeEmployeeId.Value);
                    if (emp != null) _notificationService.NotifyOperatorsAboutNewTicket(_ticket);
                }

                CommentBox.Clear();
                if (InternalCheck != null) InternalCheck.IsChecked = false;
                LoadComments();
                LoadHistory();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
        }

        private void CloseTicket_Click(object sender, RoutedEventArgs e)
        {
            var resWin = new ResolutionWindow(_ticketId, _ticket.Title) { Owner = this };
            if (resWin.ShowDialog() == true)
            {
                var t = _context.Tickets.Find(_ticketId);
                if (t != null)
                {
                    var oldStatus = t.Status;
                    t.Status = Constants.TicketStatus.Closed;
                    t.ClosedAt = DateTime.UtcNow;

                    var sol = new Solution
                    {
                        TicketId = _ticketId,
                        ResolutionText = resWin.ResolutionText,
                        ResolutionDate = DateTime.UtcNow
                    };
                    _context.Solutions.Add(sol);

                    _context.TicketHistories.Add(new TicketHistory
                    {
                        TicketId = _ticketId,
                        Action = "Закрытие",
                        Details = $"Статус: {oldStatus} -> Closed",
                        Timestamp = DateTime.UtcNow
                    });

                    _context.SaveChanges();
                    _notificationService.NotifyStatusChanged(t, oldStatus, Constants.TicketStatus.Closed);

                    LoadTicket();
                    MessageBox.Show("Тикет успешно решён", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var editWin = new EditTicketWindow(_ticketId, _context) { Owner = this };
            if (editWin.ShowDialog() == true)
                LoadTicket();
        }

        private void Rate_Click(object sender, RoutedEventArgs e)
        {
            var feedbackWindow = new FeedbackWindow() { Owner = this };
            if (feedbackWindow.ShowDialog() == true)
            {
                try
                {
                    int rating = feedbackWindow.Rating;
                    string comment = feedbackWindow.FeedbackText;

                    var feedback = new Feedback
                    {
                        TicketId = _ticketId,
                        ClientId = _ticket.ClientId,
                        SupportId = _ticket.AssigneeEmployeeId,
                        Rating = rating,
                        Comment = comment,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Feedbacks.Add(feedback);
                    _context.SaveChanges();

                    MessageBox.Show("Спасибо за ваш отзыв!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTicket();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        private void CheckAttachment()
        {
            if (AttachmentPanel == null) return;
            bool hasFile = !string.IsNullOrEmpty(_ticket.AttachmentPath);
            AttachmentPanel.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
            if (hasFile && FileNameText != null) FileNameText.Text = _ticket.AttachmentFileName;
        }

        private void CheckSolution()
        {
            if (SolutionPanel == null) return;
            bool hasSol = _ticket.Solution != null;
            SolutionPanel.Visibility = hasSol ? Visibility.Visible : Visibility.Collapsed;
            if (hasSol && SolutionText != null) SolutionText.Text = _ticket.Solution!.ResolutionText;
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateTimerDisplay();
            _timer.Start();
        }

        private void UpdateTimerDisplay()
        {
            if (_ticket == null || TimerText == null || TimerBorder == null) return;

            if (_ticket.Status == Constants.TicketStatus.Closed)
            {
                TimerBorder.Visibility = Visibility.Collapsed;
                return;
            }

            TimerBorder.Visibility = Visibility.Visible;

            if (_ticket.DueAt.HasValue)
            {
                var timeLeft = _ticket.DueAt.Value - DateTime.UtcNow;

                if (timeLeft.TotalSeconds < 0)
                {
                    TimerText.Text = $"Просрочено: {timeLeft.Duration():dd\\.hh\\:mm\\:ss}";
                    TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                }
                else
                {
                    TimerText.Text = $"Осталось: {timeLeft:dd\\.hh\\:mm\\:ss}";
                    if (timeLeft.TotalHours < 4)
                    {
                        TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5"));
                        TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C"));
                    }
                    else
                    {
                        TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                        TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
                    }
                }
            }
            else
            {
                var elapsed = DateTime.UtcNow - _ticket.CreatedAt;
                TimerText.Text = $"В работе: {elapsed:dd\\.hh\\:mm\\:ss}";
                TimerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                TimerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563"));
            }
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_ticket.AttachmentPath) && System.IO.File.Exists(_ticket.AttachmentPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = _ticket.AttachmentPath, UseShellExecute = true });
                }
            }
            catch { }
        }

        private void LinkExternal_Click(object sender, RoutedEventArgs e)
        {
            new LinkExternalDialog(_ticketId, _ticket.Title) { Owner = this }.ShowDialog();
        }

        private async void SyncExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var link = await _context.ExternalLinks.FirstOrDefaultAsync(l => l.TicketId == _ticketId);
                if (link == null)
                {
                    MessageBox.Show("Нет связи с внешней системой.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (sender is Button btn) btn.IsEnabled = false;

                using var httpClient = new HttpClient();
                var integration = new IntegrationService(_context,
                    new JiraClient(httpClient), new TrelloClient(httpClient), new BugzillaClient(), new MantisClient());

                await integration.SyncIfChangedAsync(_ticketId, link.System, System.Threading.CancellationToken.None);
                LoadTicket();
                MessageBox.Show($"Синхронизация с {link.System} выполнена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private SolidColorBrush GetStatusBrush(string status)
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