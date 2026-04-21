using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.Versioning;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class DashboardControl : UserControl, INotifyPropertyChanged
    {
        private readonly AuthService _auth;
        private readonly AuditService _audit;
        private readonly DispatcherTimer _timer;

        private List<Ticket> _periodTickets = new List<Ticket>();
        private List<Ticket> _allActiveTickets = new List<Ticket>();

        private UserUiSettings? _userSettings;
        private int _defaultPeriodDays = 30;

        private string? _currentUserName;
        public string? CurrentUserName
        {
            get => _currentUserName;
            set { _currentUserName = value; OnPropertyChanged(); }
        }

        private string? _currentUserRole;
        public string? CurrentUserRole
        {
            get => _currentUserRole;
            set { _currentUserRole = value; OnPropertyChanged(); }
        }

        private string _lastUpdatedText = "Ожидание...";
        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set { _lastUpdatedText = value; OnPropertyChanged(); }
        }

        private int _kpiTotal;
        public int KpiTotal { get => _kpiTotal; set { _kpiTotal = value; OnPropertyChanged(); } }

        private int _kpiOpen;
        public int KpiOpen { get => _kpiOpen; set { _kpiOpen = value; OnPropertyChanged(); } }

        private int _kpiInProgress;
        public int KpiInProgress { get => _kpiInProgress; set { _kpiInProgress = value; OnPropertyChanged(); } }

        private int _kpiClosed;
        public int KpiClosed { get => _kpiClosed; set { _kpiClosed = value; OnPropertyChanged(); } }

        private int _kpiOverdue;
        public int KpiOverdue { get => _kpiOverdue; set { _kpiOverdue = value; OnPropertyChanged(); } }

        private string _kpiResolvedPercentText = "0%";
        public string KpiResolvedPercentText { get => _kpiResolvedPercentText; set { _kpiResolvedPercentText = value; OnPropertyChanged(); } }

        private string _kpiAvgResolutionText = "—";
        public string KpiAvgResolutionText { get => _kpiAvgResolutionText; set { _kpiAvgResolutionText = value; OnPropertyChanged(); } }

        public SeriesCollection SeriesTicketsByDay { get; } = new SeriesCollection();
        public SeriesCollection SeriesCategories { get; } = new SeriesCollection();
        public SeriesCollection SeriesPriorities { get; } = new SeriesCollection();
        public SeriesCollection SeriesByAssignee { get; } = new SeriesCollection();

        private string[] _labelsDays = Array.Empty<string>();
        public string[] LabelsDays
        {
            get => _labelsDays;
            set { _labelsDays = value; OnPropertyChanged(); }
        }

        private string[] _labelsAssignees = Array.Empty<string>();
        public string[] LabelsAssignees
        {
            get => _labelsAssignees;
            set { _labelsAssignees = value; OnPropertyChanged(); }
        }

        public Func<double, string> CountFormatter { get; set; } = v => v.ToString("N0");

        public DashboardControl(AuthService authService)
            : this(authService, new AuditService(authService))
        {
        }

        public DashboardControl(AppDbContext context, AuthService authService)
            : this(authService, new AuditService(authService))
        {
        }

        public DashboardControl(AuthService authService, AuditService auditService)
        {
            InitializeComponent();
            DataContext = this;

            _auth = authService ?? throw new ArgumentNullException(nameof(authService));
            _audit = auditService ?? throw new ArgumentNullException(nameof(auditService));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += (s, e) => LoadFromDb();

            if (CheckUserAccess())
            {
                var user = _auth.CurrentUser;
                if (user != null)
                {
                    CurrentUserName = user.Username;
                    CurrentUserRole = user.Role;
                }

                ApplyUserSettings();
                InitializeFilters();

                Loaded += (s, e) =>
                {
                    LoadFromDb();
                    _timer.Start();
                };
                Unloaded += (s, e) => _timer.Stop();
            }
            else
            {
                Visibility = Visibility.Collapsed;
                MessageBox.Show("Нет доступа к Дашборду.");
            }
        }

        private bool CheckUserAccess()
        {
            var user = _auth.CurrentUser;
            if (user == null) return false;

            return user.Role == Constants.UserRoles.Admin ||
                   user.Role == Constants.UserRoles.Support;
        }

        private void ApplyUserSettings()
        {
            try
            {
                var userId = _auth.CurrentUser?.Id ?? 0;
                using var context = App.CreateDbContext();

                _userSettings = context.UserUiSettings
                    .FirstOrDefault(s => s.UserId == userId);

                if (_userSettings == null)
                {
                    _userSettings = new UserUiSettings { UserId = userId };
                    context.UserUiSettings.Add(_userSettings);
                    context.SaveChanges();
                }

                if (KpiSection != null)
                    KpiSection.Visibility = _userSettings.ShowKpiBlock ? Visibility.Visible : Visibility.Collapsed;

                if (ChartsSection != null)
                    ChartsSection.Visibility = _userSettings.ShowChartsBlock ? Visibility.Visible : Visibility.Collapsed;

                if (TableSection != null)
                    TableSection.Visibility = _userSettings.ShowDetailedTable ? Visibility.Visible : Visibility.Collapsed;

                if (_userSettings.RefreshRateSeconds > 0)
                    _timer.Interval = TimeSpan.FromSeconds(_userSettings.RefreshRateSeconds);
                else
                    _timer.Stop();

                _defaultPeriodDays = _userSettings.DefaultPeriodDays > 0
                    ? _userSettings.DefaultPeriodDays
                    : 30;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying settings: {ex.Message}");
                _defaultPeriodDays = 30;
            }
        }

        private void InitializeFilters()
        {
            if (ToDatePicker != null)
                ToDatePicker.SelectedDate = DateTime.Now;

            if (FromDatePicker != null)
                FromDatePicker.SelectedDate = DateTime.Now.AddDays(-_defaultPeriodDays);

            PopulateAssignees();

            var catList = new List<object> { "Все" };
            catList.AddRange(Enum.GetValues(typeof(TicketCategory)).Cast<object>());
            if (CategoryFilter != null)
                CategoryFilter.ItemsSource = catList;

            var prioList = new List<object> { "Все" };
            prioList.AddRange(Enum.GetValues(typeof(TicketPriority)).Cast<object>());
            if (PriorityFilter != null)
                PriorityFilter.ItemsSource = prioList;

            SafeSelectIndex(StatusFilter, 0);
            SafeSelectIndex(CategoryFilter, 0);
            SafeSelectIndex(PriorityFilter, 0);
            SafeSelectIndex(AssigneeFilter, 0);
            SafeSelectIndex(GroupByCombo, 0);
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var userId = _auth.CurrentUser?.Id ?? 0;

            var wnd = new UserSettingsWindow(userId)
            {
                Owner = Window.GetWindow(this)
            };

            if (wnd.ShowDialog() == true)
            {
                ApplyUserSettings();
                LoadFromDb();
            }
        }

        private void SafeSelectIndex(ComboBox? box, int index)
        {
            if (box != null)
                box.SelectedIndex = index;
        }

        private void PopulateAssignees()
        {
            try
            {
                using var context = App.CreateDbContext();

                var names = context.Employees
                    .Include(e => e.User)
                    .AsNoTracking()
                    .Where(e => e.User != null)
                    .Select(e => e.User!.Username)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                names.Insert(0, "Все");

                if (AssigneeFilter != null)
                {
                    AssigneeFilter.ItemsSource = names;
                    AssigneeFilter.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private void LoadFromDb()
        {
            try
            {
                LastUpdatedText = "Загрузка...";

                var endDate = ToDatePicker?.SelectedDate?.Date
                                  .AddDays(1)
                                  .AddSeconds(-1)
                              ?? DateTime.UtcNow;

                var startDate = FromDatePicker?.SelectedDate?.Date
                                ?? endDate.AddDays(-365);

                var startUtc = startDate.ToUniversalTime();
                var endUtc = endDate.ToUniversalTime();

                using (var db = App.CreateDbContext())
                {
                    _periodTickets = db.Tickets
                        .Include(t => t.Solution)
                        .Include(t => t.Assignee).ThenInclude(a => a.User)
                        .AsNoTracking()
                        .Where(t => t.CreatedAt >= startUtc && t.CreatedAt <= endUtc)
                        .ToList();

                    var openTickets = db.Tickets
                        .AsNoTracking()
                        .Where(t => t.Status != Constants.TicketStatus.Closed)
                        .ToList();

                    _allActiveTickets = openTickets.Union(_periodTickets.Where(t => t.Status == Constants.TicketStatus.Closed)).ToList();
                }

                UpdateDashboard();
            }
            catch (Exception ex)
            {
                LastUpdatedText = "Ошибка";
                MessageBox.Show($"Ошибка загрузки дашборда:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDashboard()
        {
            try
            {
                string statusFilter = GetComboText(StatusFilter);
                string assigneeFilter = (AssigneeFilter?.SelectedItem as string) ?? GetComboText(AssigneeFilter);
                object? catSelected = CategoryFilter?.SelectedItem;
                object? prioSelected = PriorityFilter?.SelectedItem;

                var kpiSet = ApplyFilters(_allActiveTickets, statusFilter, assigneeFilter, catSelected, prioSelected);
                CalculateKpi(kpiSet);

                // Исправлено CS8602: Гарантируем, что список не null, перед передачей
                if (_periodTickets == null) _periodTickets = new List<Ticket>();

                var categoryChartSet = ApplyFilters(_periodTickets, statusFilter, assigneeFilter, null, prioSelected);
                var priorityChartSet = ApplyFilters(_periodTickets, statusFilter, assigneeFilter, catSelected, null);
                var assigneeChartSet = ApplyFilters(_periodTickets, statusFilter, "Все", catSelected, prioSelected);

                BuildCharts(_periodTickets, categoryChartSet, priorityChartSet, assigneeChartSet);

                // Исправлено CS8602: Проверка элемента UI на null
                if (DashboardTicketsGrid != null)
                    DashboardTicketsGrid.ItemsSource = _periodTickets;

                LastUpdatedText = $"Обновлено: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dashboard Update Error: {ex.Message}");
            }
        }

        private List<Ticket> ApplyFilters(
            List<Ticket> source,
            string status,
            string assignee,
            object? category,
            object? priority)
        {
            var query = source.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(status) && status != "Все")
            {
                if (status == "In Progress")
                    query = query.Where(t => t.Status == Constants.TicketStatus.InProgress);
                else
                    query = query.Where(t => t.Status == status);
            }

            if (category is TicketCategory catVal)
                query = query.Where(t => t.Category == catVal);

            if (priority is TicketPriority prioVal)
                query = query.Where(t => t.Priority == prioVal);

            if (!string.IsNullOrWhiteSpace(assignee) && assignee != "Все")
                query = query.Where(t =>
                    t.Assignee != null &&
                    t.Assignee.User != null &&
                    t.Assignee.User.Username == assignee);

            return query.ToList();
        }

        private void CalculateKpi(List<Ticket> tickets)
        {
            int total = tickets.Count;
            int closed = tickets.Count(t => t.Status == Constants.TicketStatus.Closed);
            int open = tickets.Count(t => t.Status == Constants.TicketStatus.Open);
            int inProgress = tickets.Count(t => t.Status == Constants.TicketStatus.InProgress);
            int overdue = tickets.Count(t => t.IsOverdue);

            KpiTotal = total;
            KpiOpen = open;
            KpiInProgress = inProgress;
            KpiClosed = closed;
            KpiOverdue = overdue;

            double percent = total == 0 ? 0 : (double)closed / total * 100.0;
            KpiResolvedPercentText = $"{percent:0.#}%";

            var resolvedTimes = tickets
                .Where(t => t.Status == Constants.TicketStatus.Closed && t.ClosedAt.HasValue)
                .Select(t => (t.ClosedAt!.Value - t.CreatedAt).TotalHours)
                .ToList();

            if (resolvedTimes.Any())
            {
                double avgHours = resolvedTimes.Average();
                KpiAvgResolutionText = avgHours < 24
                    ? $"{avgHours:0.#} ч"
                    : $"{(avgHours / 24):0.#} дн";
            }
            else
            {
                KpiAvgResolutionText = "—";
            }
        }

        private void BuildCharts(
            List<Ticket> periodSet,
            List<Ticket> catSet,
            List<Ticket> prioSet,
            List<Ticket> assignSet)
        {
            var groupType = GetComboText(GroupByCombo);

            var grouped = periodSet
                .GroupBy(t =>
                    groupType == "Месяцы"
                        ? new DateTime(t.CreatedAt.Year, t.CreatedAt.Month, 1)
                        : t.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() });

            if (SortDaysCombo?.SelectedIndex == 1)
                grouped = grouped.OrderByDescending(x => x.Date);
            else
                grouped = grouped.OrderBy(x => x.Date);

            var groupedList = grouped.ToList();

            SeriesTicketsByDay.Clear();
            if (groupedList.Any())
            {
                var format = groupType == "Месяцы" ? "MMM yy" : "dd.MM";
                LabelsDays = groupedList.Select(x => x.Date.ToString(format)).ToArray();

                SeriesTicketsByDay.Add(new ColumnSeries
                {
                    Title = "Тикеты",
                    Values = new ChartValues<int>(groupedList.Select(x => x.Count)),
                    DataLabels = true
                });
            }
            else
            {
                LabelsDays = new[] { "-" };
                SeriesTicketsByDay.Add(new ColumnSeries
                {
                    Values = new ChartValues<int> { 0 },
                    DataLabels = false
                });
            }

            var catGroups = catSet
                .GroupBy(t => t.Category)
                .Select(g => new { Name = g.Key.ToString(), Count = g.Count() });

            if (CategorySortCombo?.SelectedIndex == 1)
                catGroups = catGroups.OrderBy(x => x.Name);
            else
                catGroups = catGroups.OrderByDescending(x => x.Count);

            SeriesCategories.Clear();
            if (!catGroups.Any())
            {
                SeriesCategories.Add(new PieSeries
                {
                    Title = "Нет данных",
                    Values = new ChartValues<int> { 1 },
                    DataLabels = false,
                    Fill = Brushes.LightGray
                });
            }
            else
            {
                foreach (var g in catGroups)
                {
                    SeriesCategories.Add(new PieSeries
                    {
                        Title = g.Name,
                        Values = new ChartValues<int> { g.Count },
                        DataLabels = true
                    });
                }
            }

            var prioGroups = prioSet
                .GroupBy(t => t.Priority)
                .Select(g => new
                {
                    Name = g.Key.ToString(),
                    Count = g.Count(),
                    Val = (int)g.Key
                });

            if (PrioritySortCombo?.SelectedIndex == 1)
                prioGroups = prioGroups.OrderByDescending(x => x.Val);
            else
                prioGroups = prioGroups.OrderByDescending(x => x.Count);

            SeriesPriorities.Clear();
            if (!prioGroups.Any())
            {
                SeriesPriorities.Add(new PieSeries
                {
                    Title = "Нет данных",
                    Values = new ChartValues<int> { 1 },
                    DataLabels = false,
                    Fill = Brushes.LightGray
                });
            }
            else
            {
                foreach (var g in prioGroups)
                {
                    SeriesPriorities.Add(new PieSeries
                    {
                        Title = g.Name,
                        Values = new ChartValues<int> { g.Count },
                        DataLabels = true
                    });
                }
            }

            var assignGroups = assignSet
                .GroupBy(t => t.Assignee?.User?.Username ?? "Не назначен")
                .Select(g => new { Name = g.Key, Count = g.Count() });

            if (AssigneeSortCombo?.SelectedIndex == 1)
                assignGroups = assignGroups.OrderBy(x => x.Name);
            else
                assignGroups = assignGroups.OrderByDescending(x => x.Count);

            if (TopAssigneesCombo?.SelectedIndex == 1)
                assignGroups = assignGroups.Take(5);

            var assignList = assignGroups.ToList();
            LabelsAssignees = assignList.Select(x => x.Name).ToArray();

            SeriesByAssignee.Clear();
            if (!assignList.Any())
            {
                LabelsAssignees = new[] { "Нет данных" };
                SeriesByAssignee.Add(new RowSeries
                {
                    Values = new ChartValues<int> { 0 },
                    DataLabels = false
                });
            }
            else
            {
                SeriesByAssignee.Add(new RowSeries
                {
                    Title = "Тикеты",
                    Values = new ChartValues<int>(assignList.Select(x => x.Count)),
                    DataLabels = true
                });
            }
        }

        private string GetComboText(ComboBox? box)
        {
            if (box == null) return string.Empty;

            if (box.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? string.Empty;

            return box.SelectedItem?.ToString() ?? string.Empty;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tickets = DashboardTicketsGrid.ItemsSource as IEnumerable<Ticket>;
                if (tickets == null || !tickets.Any())
                {
                    MessageBox.Show("Нет данных для экспорта.");
                    return;
                }

                var dlg = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"Dashboard_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ID;Title;Status;Category;Priority;Assignee;CreatedAt;DueAt");

                    foreach (var t in tickets)
                    {
                        sb.AppendLine(
                            $"{t.Id};\"{t.Title}\";{t.Status};{t.Category};{t.Priority};" +
                            $"{t.Assignee?.User?.Username};{t.CreatedAt};{t.DueAt}");
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);

                    if (MessageBox.Show("CSV файл сохранен. Открыть?",
                            "Экспорт", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = dlg.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Filter = "PDF Files|*.pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    GeneratePdfReport(dlg.FileName);
                    MessageBox.Show("Отчет сохранен!", "Экспорт",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dlg.FileName,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GeneratePdfReport(string filename)
        {
            using var document = new PdfDocument();
            document.Info.Title = "Analytics Report";

            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var fontTitle = new XFont("Verdana", 20, XFontStyle.Bold);
            var fontHeader = new XFont("Verdana", 12, XFontStyle.Bold);
            var fontNormal = new XFont("Verdana", 10, XFontStyle.Regular);
            var fontSmall = new XFont("Verdana", 8, XFontStyle.Regular);

            gfx.DrawString("Отчет по аналитике", fontTitle, XBrushes.DarkBlue,
                new XRect(0, 20, page.Width, 40), XStringFormats.TopCenter);

            gfx.DrawString($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}",
                fontNormal, XBrushes.Black, 40, 60);

            gfx.DrawString($"Сотрудник: {CurrentUserName}",
                fontNormal, XBrushes.Black, 40, 75);

            double y = 110;

            DrawKpiBox(gfx, "Всего", KpiTotal.ToString(), 40, y);
            DrawKpiBox(gfx, "Открыто", KpiOpen.ToString(), 130, y);
            DrawKpiBox(gfx, "В работе", KpiInProgress.ToString(), 220, y);
            DrawKpiBox(gfx, "Закрыто", KpiClosed.ToString(), 310, y);
            DrawKpiBox(gfx, "Просрочено", KpiOverdue.ToString(), 400, y, XBrushes.Red);
            DrawKpiBox(gfx, "Ср. время", KpiAvgResolutionText, 490, y);

            y += 60;
            gfx.DrawLine(XPens.Gray, 40, y, page.Width - 40, y);
            y += 20;

            gfx.DrawString("Динамика:", fontHeader, XBrushes.Black, 40, y);
            var imgDynamics = CaptureControl(ChartDynamics);
            if (imgDynamics != null)
            {
                gfx.DrawImage(imgDynamics, 40, y + 25, 500, 200);
                y += 240;
            }
            else
            {
                y += 40;
            }

            gfx.DrawString("Категории:", fontHeader, XBrushes.Black, 40, y);
            var imgCats = CaptureControl(ChartCategories);
            if (imgCats != null)
            {
                gfx.DrawImage(imgCats, 40, y + 25, 250, 180);
            }

            var imgPrio = CaptureControl(ChartPriorities);
            if (imgPrio != null)
            {
                gfx.DrawString("Приоритеты:", fontHeader, XBrushes.Black, 310, y);
                gfx.DrawImage(imgPrio, 310, y + 25, 250, 180);
            }

            y += 220;

            if (_userSettings == null || _userSettings.ShowDetailedTable)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = 40;

                gfx.DrawString("Список тикетов (выборка):",
                    fontHeader, XBrushes.Black, 40, y);

                y += 30;

                gfx.DrawString("#", fontHeader, XBrushes.Black, 40, y);
                gfx.DrawString("Тема", fontHeader, XBrushes.Black, 80, y);
                gfx.DrawString("Статус", fontHeader, XBrushes.Black, 300, y);
                gfx.DrawString("Исполнитель", fontHeader, XBrushes.Black, 400, y);

                y += 5;
                gfx.DrawLine(XPens.Black, 40, y + 10, page.Width - 40, y + 10);
                y += 20;

                var tickets = DashboardTicketsGrid.ItemsSource as IEnumerable<Ticket>;
                if (tickets != null)
                {
                    foreach (var t in tickets)
                    {
                        if (y > page.Height - 40)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            y = 40;
                        }

                        gfx.DrawString(t.Id.ToString(), fontSmall, XBrushes.Black, 40, y);

                        string title = t.Title.Length > 40
                            ? t.Title.Substring(0, 37) + "..."
                            : t.Title;

                        gfx.DrawString(title, fontSmall, XBrushes.Black, 80, y);
                        gfx.DrawString(t.Status, fontSmall, XBrushes.Black, 300, y);
                        gfx.DrawString(t.Assignee?.User?.Username ?? "—",
                            fontSmall, XBrushes.Black, 400, y);

                        y += 15;
                    }
                }
            }

            document.Save(filename);
        }

        private XImage? CaptureControl(UIElement source)
        {
            try
            {
                double actualHeight = source.RenderSize.Height;
                double actualWidth = source.RenderSize.Width;

                if (actualHeight <= 0 || actualWidth <= 0)
                    return null;

                var renderTarget = new RenderTargetBitmap(
                    (int)actualWidth,
                    (int)actualHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);

                renderTarget.Render(source);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using var stream = new MemoryStream();
                encoder.Save(stream);
                var bytes = stream.ToArray();

                return XImage.FromStream(() => new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        private void DrawKpiBox(
            XGraphics gfx,
            string label,
            string value,
            double x,
            double y,
            XBrush? valueBrush = null)
        {
            var fontLabel = new XFont("Verdana", 8, XFontStyle.Regular);
            var fontValue = new XFont("Verdana", 12, XFontStyle.Bold);

            valueBrush ??= XBrushes.Black;

            gfx.DrawString(label, fontLabel, XBrushes.Gray, x, y);
            gfx.DrawString(value, fontValue, valueBrush, x, y + 12);
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => LoadFromDb();

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            InitializeFilters();
            LoadFromDb();
        }

        private void Quick7_Click(object sender, RoutedEventArgs e)
        {
            if (FromDatePicker != null)
                FromDatePicker.SelectedDate = DateTime.Now.AddDays(-7);

            LoadFromDb();
        }

        private void Quick30_Click(object sender, RoutedEventArgs e)
        {
            if (FromDatePicker != null)
                FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);

            LoadFromDb();
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) UpdateDashboard();
        }

        private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) UpdateDashboard();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}