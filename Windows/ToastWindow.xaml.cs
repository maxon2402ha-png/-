using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class ToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly int? _ticketId;
        private Storyboard? _progressStoryboard;
        private bool _isPaused = false;

        /// <summary>
        /// Конструктор по умолчанию
        /// </summary>
        public ToastWindow()
        {
            InitializeComponent();

            // Fallback таймер автозакрытия (если анимация прогресс-бара не сработает)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                CloseWithAnimation();
            };
        }

        /// <summary>
        /// Конструктор только с заголовком и сообщением
        /// </summary>
        public ToastWindow(string title, string message)
            : this(title, message, "Info", null)
        {
        }

        /// <summary>
        /// Основной конструктор со строковым типом
        /// </summary>
        public ToastWindow(string title, string message, string type = "Info", int? ticketId = null)
            : this()
        {
            _ticketId = ticketId;
            Tag = ticketId;

            if (TitleText != null)
                TitleText.Text = title ?? string.Empty;

            if (MessageText != null)
                MessageText.Text = message ?? string.Empty;

            // Настройка стиля по типу
            ApplyTypeStyle(type ?? "Info");

            // Позиционирование
            PositionWindow();
        }

        private void ApplyTypeStyle(string type)
        {
            string backgroundColor;
            string iconKey;

            switch (type.ToLowerInvariant())
            {
                case "success":
                case "closed":
                    backgroundColor = "#10B981"; // Green
                    iconKey = "Icon.Check";
                    break;
                case "warning":
                case "duesoon":
                    backgroundColor = "#F59E0B"; // Orange
                    iconKey = "Icon.Warning";
                    break;
                case "error":
                case "overdue":
                    backgroundColor = "#EF4444"; // Red
                    iconKey = "Icon.Error";
                    break;
                case "info":
                default:
                    backgroundColor = "#2563EB"; // Blue
                    iconKey = "Icon.Bell";
                    break;
            }

            try
            {
                // Устанавливаем цвет фона иконки
                if (IconBorder != null)
                {
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
                }

                // Устанавливаем иконку (Path геометрию)
                if (IconPath != null && TryFindResource(iconKey) is Geometry geometry)
                {
                    IconPath.Data = geometry;
                }

                // Прогресс-бар
                if (ProgressBar != null)
                {
                    ProgressBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toast ApplyTypeStyle error: {ex.Message}");
            }
        }

        private void PositionWindow()
        {
            try
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = desktopWorkingArea.Right - this.Width - 16;
                this.Top = desktopWorkingArea.Bottom - this.Height - 16;
            }
            catch
            {
                this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
                this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 100;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Запускаем анимацию появления
                if (TryFindResource("SlideIn") is Storyboard slideIn)
                {
                    slideIn.Begin(this);
                }

                // Анимация прогресс-бара (автозакрытие через 5 сек)
                if (TryFindResource("ProgressAnimation") is Storyboard progressAnim)
                {
                    _progressStoryboard = progressAnim;
                    _progressStoryboard.Completed += (s, args) => CloseWithAnimation();
                    _progressStoryboard.Begin(this);
                }
                else
                {
                    // Fallback - обычный таймер
                    _timer.Start();
                }

                // Звук уведомления
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toast animation error: {ex.Message}");
                _timer.Start();
            }
        }

        // ===================== СОБЫТИЯ МЫШИ =====================

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            // Пауза автозакрытия при наведении
            if (_progressStoryboard != null && !_isPaused)
            {
                try
                {
                    _progressStoryboard.Pause(this);
                    _isPaused = true;
                }
                catch { }
            }

            _timer?.Stop();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            // Возобновление автозакрытия
            if (_progressStoryboard != null && _isPaused)
            {
                try
                {
                    _progressStoryboard.Resume(this);
                    _isPaused = false;
                }
                catch { }
            }

            // Не перезапускаем таймер, если прогресс-анимация работает
            if (_progressStoryboard == null)
            {
                _timer?.Start();
            }
        }

        private void Toast_Click(object sender, MouseButtonEventArgs e)
        {
            // Переход к связанному тикету
            int? ticketId = _ticketId ?? (Tag as int?);

            if (ticketId.HasValue)
            {
                try
                {
                    var detailsWindow = new TicketDetailsWindow(ticketId.Value);
                    detailsWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка открытия тикета: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            CloseWithAnimation();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _progressStoryboard?.Stop(this);
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            try
            {
                _progressStoryboard?.Stop(this);
                _timer?.Stop();

                if (TryFindResource("FadeOut") is Storyboard fadeOut)
                {
                    fadeOut.Begin(this);
                }
                else
                {
                    Close();
                }
            }
            catch
            {
                Close();
            }
        }

        private void FadeOut_Completed(object sender, EventArgs e)
        {
            Close();
        }

        // ===================== СТАТИЧЕСКИЕ МЕТОДЫ =====================

        public static void Show(string title, string message, string type = "Info", int? ticketId = null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    var toast = new ToastWindow(title, message, type, ticketId);
                    toast.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Toast show error: {ex.Message}");
                }
            });
        }

        public static void ShowInfo(string title, string message, int? ticketId = null)
            => Show(title, message, "Info", ticketId);

        public static void ShowSuccess(string title, string message, int? ticketId = null)
            => Show(title, message, "Success", ticketId);

        public static void ShowWarning(string title, string message, int? ticketId = null)
            => Show(title, message, "Warning", ticketId);

        public static void ShowError(string title, string message, int? ticketId = null)
            => Show(title, message, "Error", ticketId);
    }
}