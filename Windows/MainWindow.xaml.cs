using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.Versioning;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;
using AppConstants = КР_Ханников.Core.Constants;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly NotificationService _notificationService;
        private readonly TicketService _ticketService;
        private DispatcherTimer? _notificationTimer;
        private DispatcherTimer? _syncTimer;

        // Для отмены предыдущего запроса поиска при быстром вводе
        private CancellationTokenSource? _searchCts;
        private bool _isLoading = false;

        public User? CurrentUser => _authService?.CurrentUser;

        public MainWindow(AppDbContext context, AuthService authService)
        {
            InitializeComponent();

            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _notificationService = new NotificationService(_context, _authService);
            _ticketService = new TicketService(_context);

            Debug.WriteLine($"[MainWindow] Инициализация для: {_authService.CurrentUser?.Username}");

            InitializeFilters();
            DataContext = this;
            ConfigureAccess();

            // Первичная загрузка
            bool isClient = CurrentUser?.Role == AppConstants.UserRoles.Client;
            if (isClient) UpdateSidebar(MyTicketsButton); else UpdateSidebar(AllTicketsButton);

            // Запускаем асинхронно
            _ = LoadTicketsAsync(isClient);

            LoadSavedSearches();

            _notificationService.ShowUnreadNotificationsForCurrentUser();
            UpdateNotificationsButtonCaption();
            StartTimers();
        }

        private void InitializeFilters()
        {
            if (CategoryFilter != null)
            {
                var cats = new List<object> { "Все" };
                cats.AddRange(Enum.GetValues(typeof(TicketCategory)).Cast<object>());
                CategoryFilter.ItemsSource = cats;
                CategoryFilter.SelectedIndex = 0;
            }

            if (PriorityFilter != null)
            {
                var prios = new List<object> { "Все" };
                prios.AddRange(Enum.GetValues(typeof(TicketPriority)).Cast<object>());
                PriorityFilter.ItemsSource = prios;
                PriorityFilter.SelectedIndex = 0;
            }
        }

        private void StartTimers()
        {
            _notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(AppConstants.UI.NotificationCheckIntervalMinutes)
            };
            _notificationTimer.Tick += (s, e) =>
            {
                try
                {
                    _notificationService.CheckDueSoonTicketsForCurrentUser();
                    UpdateNotificationsButtonCaption();
                }
                catch { }
            };
            _notificationTimer.Start();

            _syncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _syncTimer.Tick += async (s, e) =>
            {
                try
                {
                    using var syncContext = App.CreateDbContext();
                    using var httpClient = new System.Net.Http.HttpClient();
                    var syncService = new IntegrationService(
                        syncContext,
                        new JiraClient(httpClient),
                        new TrelloClient(httpClient),
                        new BugzillaClient(),
                        new MantisClient());

                    await syncService.SyncAllActiveLinksAsync(CancellationToken.None);

                    if (TicketsContent != null && TicketsContent.Visibility == Visibility.Visible)
                        await LoadTicketsAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoSync] {ex.Message}");
                }
            };
            _syncTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _notificationTimer?.Stop();
            _syncTimer?.Stop();
            base.OnClosed(e);
        }

        private void UpdateSidebar(Button? activeButton)
        {
            var buttons = new[] {
                OpenDashboardButton, OpenAnalyticsButton, MyTicketsButton, AllTicketsButton,
                ClientHistoryButton, ClosedTicketsButton, EmployeesButton,
                IntegrationButton, AuditButton, OpenKnowledgeBaseButton
            };

            foreach (var btn in buttons)
            {
                if (btn != null) btn.Style = (Style)FindResource("NavButton");
            }

            if (activeButton != null) activeButton.Style = (Style)FindResource("NavButtonActive");
        }

        private void SwitchPage(Button btn, UserControl content)
        {
            UpdateSidebar(btn);
            if (TicketsContent != null) TicketsContent.Visibility = Visibility.Collapsed;
            if (PagesContent != null)
            {
                PagesContent.Visibility = Visibility.Visible;
                PagesContent.Content = content;
            }
        }

        // --- Обработчики меню (навигация) ---
        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
            => SwitchPage(OpenDashboardButton, new DashboardControl(_authService));

        // Окно аналитики KPI и нагрузки. В отличие от Дашборда это полноценное
        // Window, а не UserControl, поэтому открывается модально через ShowDialog,
        // а не через SwitchPage.
        private void OpenAnalytics_Click(object sender, RoutedEventArgs e)
        {
            var window = new AnalyticsWindow { Owner = this };
            window.ShowDialog();
        }

        private void OpenKnowledgeBase_Click(object sender, RoutedEventArgs e)
            => SwitchPage(OpenKnowledgeBaseButton, new KnowledgeBaseControl(_context, _authService));

        private void OpenClientHistory_Click(object sender, RoutedEventArgs e)
            => SwitchPage(ClientHistoryButton, new ClientHistoryControl(_context, _authService));

        private void OpenEmployees_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser?.Role == AppConstants.UserRoles.Admin)
                SwitchPage(EmployeesButton, new ManageEmployeesControl());
            else
                MessageBox.Show("Доступ запрещен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void OpenIntegrationSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser?.Role == AppConstants.UserRoles.Admin)
                SwitchPage(IntegrationButton, new IntegrationSettingsControl(_context, _authService));
            else
                MessageBox.Show("Доступ запрещен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void OpenAudit_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser?.Role == AppConstants.UserRoles.Admin)
                SwitchPage(AuditButton, new AuditLogControl(_context, _authService));
            else
                MessageBox.Show("Доступ запрещен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public async void OpenTickets_Click(object sender, RoutedEventArgs e)
        {
            bool isClient = CurrentUser?.Role == AppConstants.UserRoles.Client;
            var btn = isClient ? MyTicketsButton : AllTicketsButton;
            UpdateSidebar(btn);

            if (TicketsContent != null) TicketsContent.Visibility = Visibility.Visible;
            if (PagesContent != null)
            {
                PagesContent.Visibility = Visibility.Collapsed;
                PagesContent.Content = null;
            }
            await LoadTicketsAsync(isClient);
        }

        private async void OpenMyTickets_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebar(MyTicketsButton);
            ShowTickets();
            await LoadTicketsAsync(true);
        }

        private async void OpenAllTickets_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebar(AllTicketsButton);
            ShowTickets();
            await LoadTicketsAsync(false);
        }

        private async void OpenClosedTickets_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebar(ClosedTicketsButton);
            ShowTickets();

            if (StatusFilter != null)
            {
                foreach (ComboBoxItem item in StatusFilter.Items)
                {
                    if (item.Content?.ToString() == "Closed")
                    {
                        StatusFilter.SelectedItem = item;
                        break;
                    }
                }
            }
            await LoadTicketsAsync(false);
        }

        private void ShowTickets()
        {
            if (TicketsContent != null) TicketsContent.Visibility = Visibility.Visible;
            if (PagesContent != null) PagesContent.Visibility = Visibility.Collapsed;
        }

        // --- ЛОГИКА ЗАГРУЗКИ (АСИНХРОННАЯ) ---
        public async Task LoadTicketsAsync(bool filterByCurrentUser = false, string? searchQuery = null)
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                // Очистка трекера для получения свежих данных
                _context.ChangeTracker.Clear();

                var currentUser = _authService.CurrentUser;
                if (currentUser == null) return;

                // Строим запрос
                var query = _context.Tickets
                    .Include(t => t.Client)
                    .Include(t => t.Assignee).ThenInclude(a => a != null ? a.User : null)
                    .Include(t => t.Solution)
                    .AsNoTracking()
                    .AsQueryable();

                // Фильтрация по правам доступа
                if (currentUser.Role == AppConstants.UserRoles.Client)
                {
                    var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
                    if (client != null) query = query.Where(t => t.ClientId == client.Id);
                    else query = query.Where(t => false); // Нет клиента - нет тикетов
                }
                else if (currentUser.Role == AppConstants.UserRoles.Support && filterByCurrentUser)
                {
                    var empId = await GetCurrentEmployeeIdOrNullAsync();
                    query = query.Where(t => t.AssigneeEmployeeId == empId);
                }

                // Поиск
                var effectiveQuery = searchQuery ?? SearchBox?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(effectiveQuery))
                {
                    var term = effectiveQuery.ToLower();
                    query = query.Where(t =>
                        (t.Title != null && t.Title.ToLower().Contains(term)) ||
                        (t.Description != null && t.Description.ToLower().Contains(term)) ||
                        (t.Solution != null && t.Solution.ResolutionText != null && t.Solution.ResolutionText.ToLower().Contains(term)) ||
                        (t.Client != null && t.Client.Name.ToLower().Contains(term)) ||
                        t.Id.ToString().Contains(term));
                }

                // Фильтры UI
                if (StatusFilter?.SelectedItem is ComboBoxItem sel && sel.Content is string st && st != "Все статусы" && st != "Все")
                {
                    if (st == "In Progress") query = query.Where(t => t.Status == AppConstants.TicketStatus.InProgress);
                    else query = query.Where(t => t.Status == st);
                }

                if (CategoryFilter?.SelectedItem is TicketCategory cat)
                    query = query.Where(t => t.Category == cat);

                if (PriorityFilter?.SelectedItem is TicketPriority pr)
                    query = query.Where(t => t.Priority == pr);

                if (CreatedFromPicker?.SelectedDate is DateTime from)
                    query = query.Where(t => t.CreatedAt >= from.Date.ToUniversalTime());

                if (CreatedToPicker?.SelectedDate is DateTime to)
                    query = query.Where(t => t.CreatedAt < to.Date.AddDays(1).ToUniversalTime());

                // Сортировка и выполнение запроса асинхронно
                var tickets = await query
                    .OrderByDescending(t => t.Priority)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                if (TicketsGrid != null) TicketsGrid.ItemsSource = tickets;
                if (TicketCountText != null) TicketCountText.Text = $"{tickets.Count} заявок";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Обработка ввода с задержкой (Debounce)
        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MyTicketsButton == null) return;
            bool myOnly = MyTicketsButton.Style == (Style)FindResource("NavButtonActive");

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(500, token); // Ждем 500мс
                await LoadTicketsAsync(myOnly, SearchBox?.Text?.Trim());
            }
            catch (TaskCanceledException) { /* Игнорируем отмену */ }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
            => await LoadTicketsAsync(searchQuery: SearchBox?.Text?.Trim());

        private async void CreateTicket_Click(object sender, RoutedEventArgs e)
        {
            if (new CreateTicketWindow(_context, _authService).ShowDialog() == true)
            {
                if (MyTicketsButton == null) return;
                bool myOnly = MyTicketsButton.Style == (Style)FindResource("NavButtonActive");
                await LoadTicketsAsync(myOnly);
            }
        }

        private async void EditTicket_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsGrid?.SelectedItem is Ticket s)
            {
                var detailsWindow = new TicketDetailsWindow(s, _context, _authService) { Owner = this };
                detailsWindow.ShowDialog();
                if (MyTicketsButton == null) return;
                bool myOnly = MyTicketsButton.Style == (Style)FindResource("NavButtonActive");
                await LoadTicketsAsync(myOnly);
            }
        }

        private async void TicketsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TicketsGrid?.SelectedItem is Ticket s)
            {
                var detailsWindow = new TicketDetailsWindow(s, _context, _authService) { Owner = this };
                detailsWindow.ShowDialog();
                if (MyTicketsButton == null) return;
                bool myOnly = MyTicketsButton.Style == (Style)FindResource("NavButtonActive");
                await LoadTicketsAsync(myOnly);
            }
        }

        private async void AssignTicket_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsGrid?.SelectedItem is Ticket s)
            {
                var eid = await GetCurrentEmployeeIdOrNullAsync();
                if (eid != null)
                {
                    await _ticketService.AssignAsync(s.Id, eid.Value);
                    await LoadTicketsAsync();
                }
            }
        }

        private async void DeleteTicket_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsGrid?.SelectedItem is Ticket s)
            {
                if (MessageBox.Show("Удалить?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _ticketService.DeleteAsync(s.Id);
                    await LoadTicketsAsync();
                }
            }
        }

        // Вспомогательные методы
        private void LoadSavedSearches()
        {
            var current = _authService.CurrentUser;
            if (current == null || SavedSearchComboBox == null) return;

            try
            {
                var presets = _context.SearchPresets.AsNoTracking().Where(p => p.UserId == current.Id).OrderBy(p => p.Name).ToList();
                SavedSearchComboBox.ItemsSource = presets;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки пресетов: {ex.Message}");
            }
        }

        private void SaveSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var current = _authService.CurrentUser;
            if (current == null) return;

            string name = Interaction.InputBox("Введите название для фильтра:", "Сохранение поиска", "Мой фильтр");
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var preset = new SearchPreset
                {
                    UserId = current.Id,
                    Name = name.Trim(),
                    TextQuery = SearchBox?.Text?.Trim(),
                    CreatedFrom = CreatedFromPicker.SelectedDate,
                    CreatedTo = CreatedToPicker.SelectedDate
                };

                if (StatusFilter.SelectedItem is ComboBoxItem si && si.Content is string st && st != "Все" && st != "Все статусы") preset.Status = st;
                if (CategoryFilter.SelectedItem is TicketCategory cat) preset.Category = cat;
                if (PriorityFilter.SelectedItem is TicketPriority prio) preset.Priority = prio;

                _context.SearchPresets.Add(preset);
                _context.SaveChanges();
                LoadSavedSearches();
                MessageBox.Show("Фильтр сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void SavedSearchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SavedSearchComboBox.SelectedItem is not SearchPreset preset) return;

            if (SearchBox != null) SearchBox.Text = preset.TextQuery ?? string.Empty;
            if (CreatedFromPicker != null) CreatedFromPicker.SelectedDate = preset.CreatedFrom;
            if (CreatedToPicker != null) CreatedToPicker.SelectedDate = preset.CreatedTo;

            if (!string.IsNullOrEmpty(preset.Status) && StatusFilter != null)
            {
                foreach (ComboBoxItem item in StatusFilter.Items)
                {
                    if (item.Content?.ToString() == preset.Status)
                    {
                        StatusFilter.SelectedItem = item;
                        break;
                    }
                }
            }
            else if (StatusFilter != null) StatusFilter.SelectedIndex = 0;

            if (preset.Category.HasValue && CategoryFilter != null) CategoryFilter.SelectedItem = preset.Category.Value;
            if (preset.Priority.HasValue && PriorityFilter != null) PriorityFilter.SelectedItem = preset.Priority.Value;

            _ = LoadTicketsAsync();
        }

        private void DeleteSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SavedSearchComboBox.SelectedItem is not SearchPreset preset)
            {
                MessageBox.Show("Выберите фильтр.", "Внимание");
                return;
            }

            if (MessageBox.Show($"Удалить '{preset.Name}'?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                var entity = _context.SearchPresets.FirstOrDefault(p => p.Id == preset.Id);
                if (entity != null)
                {
                    _context.SearchPresets.Remove(entity);
                    _context.SaveChanges();
                    LoadSavedSearches();
                    SavedSearchComboBox.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        // Export Csv/Pdf - (Код экспорта аналогичен предыдущему, но методы асинхронными делать не обязательно, 
        // так как экспорт происходит из уже загруженного ItemsSource, что быстро)
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tickets = TicketsGrid.ItemsSource as IEnumerable<Ticket>;
                if (tickets == null || !tickets.Any()) { MessageBox.Show("Нет данных."); return; }

                var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"Tickets_{DateTime.Now:yyyyMMdd}.csv" };
                if (dlg.ShowDialog() == true)
                {
                    var sb = new StringBuilder("ID;Title;Status;Category;Priority;Assignee;CreatedAt\n");
                    foreach (var t in tickets)
                        sb.AppendLine($"{t.Id};{t.Title};{t.Status};{t.Category};{t.Priority};{t.Assignee?.User?.Username};{t.CreatedAt}");

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Экспорт завершен.");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            // Аналогичный код экспорта PDF
            try
            {
                var tickets = TicketsGrid.ItemsSource as IEnumerable<Ticket>;
                if (tickets == null || !tickets.Any()) { MessageBox.Show("Нет данных."); return; }

                var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Tickets_{DateTime.Now:yyyyMMdd}.pdf" };
                if (dlg.ShowDialog() == true)
                {
                    using var document = new PdfDocument();
                    document.Info.Title = "Список тикетов";
                    var page = document.AddPage();
                    var gfx = XGraphics.FromPdfPage(page);
                    var font = new XFont("Verdana", 10, XFontStyle.Regular);
                    double y = 40;

                    foreach (var t in tickets)
                    {
                        if (y > page.Height - 40) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); y = 40; }
                        gfx.DrawString($"#{t.Id} {t.Title} [{t.Status}]", font, XBrushes.Black, 40, y);
                        y += 20;
                    }
                    document.Save(dlg.FileName);
                    MessageBox.Show("PDF создан.");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void OpenNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser != null)
            {
                new NotificationCenterWindow(_authService.CurrentUser.Id) { Owner = this }.ShowDialog();
                UpdateNotificationsButtonCaption();
            }
        }

        private void OpenNotificationSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser != null)
                new NotificationSettingsWindow(_authService.CurrentUser.Id) { Owner = this }.ShowDialog();
        }

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser != null)
                new UserProfileWindow(_authService.CurrentUser.Id) { Owner = this }.ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _authService.Logout();
            new LoginWindow(new AppDbContext(), new AuthService(new AppDbContext())).Show();
            Close();
        }

        private void UpdateNotificationsButtonCaption()
        {
            if (OpenNotificationsButton == null || _authService.CurrentUser == null) return;
            try
            {
                var count = _context.Notifications.Count(n => n.UserId == _authService.CurrentUser.Id && !n.IsRead);
                OpenNotificationsButton.Content = count > 0 ? $"🔔 ({count})" : "🔔";
            }
            catch { }
        }

        private void ConfigureAccess()
        {
            var role = _authService.CurrentUser?.Role;
            bool isAdmin = role == AppConstants.UserRoles.Admin;
            bool isClient = role == AppConstants.UserRoles.Client;

            if (EmployeesButton != null) EmployeesButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (IntegrationButton != null) IntegrationButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (AuditButton != null) AuditButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (CreateTicketButton != null) CreateTicketButton.Visibility = isClient ? Visibility.Visible : Visibility.Collapsed;

            if (isClient)
            {
                if (OpenDashboardButton != null) OpenDashboardButton.Visibility = Visibility.Collapsed;
                if (OpenKnowledgeBaseButton != null) OpenKnowledgeBaseButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<int?> GetCurrentEmployeeIdOrNullAsync()
        {
            var u = _authService.CurrentUser;
            if (u == null) return null;
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == u.Id);
            return emp?.Id;
        }
    }
}