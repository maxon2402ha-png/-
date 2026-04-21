using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class FeedbackWindow : Window
    {
        // Свойство для хранения текста отзыва
        public string FeedbackText { get; private set; } = string.Empty;

        // Свойство для хранения оценки (по умолчанию 0, пока не выберут)
        public int Rating { get; private set; } = 0;

        public FeedbackWindow()
        {
            InitializeComponent();

            // Ставим фокус на поле ввода после загрузки
            Loaded += (s, e) => FeedbackTextBox.Focus();
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int rating))
            {
                Rating = rating;
                UpdateStarsVisual(rating);

                // Разблокируем кнопку отправки, так как оценка выбрана
                if (SubmitButton != null)
                    SubmitButton.IsEnabled = true;
            }
        }

        // Метод для визуального обновления звезд (закрашивание)
        private void UpdateStarsVisual(int rating)
        {
            var activeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Золотой
            var inactiveColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")); // Серый

            // Обновляем цвет каждой звезды вручную
            if (Star1 != null) Star1.Foreground = rating >= 1 ? activeColor : inactiveColor;
            if (Star2 != null) Star2.Foreground = rating >= 2 ? activeColor : inactiveColor;
            if (Star3 != null) Star3.Foreground = rating >= 3 ? activeColor : inactiveColor;
            if (Star4 != null) Star4.Foreground = rating >= 4 ? activeColor : inactiveColor;
            if (Star5 != null) Star5.Foreground = rating >= 5 ? activeColor : inactiveColor;

            // Обновляем текстовое описание оценки
            if (RatingText != null)
            {
                RatingText.Text = rating switch
                {
                    1 => "Очень плохо",
                    2 => "Плохо",
                    3 => "Нормально",
                    4 => "Хорошо",
                    5 => "Отлично",
                    _ => "Выберите оценку"
                };
            }

            // Обновляем скрытое поле (если оно используется для привязок)
            if (RatingValue != null)
                RatingValue.Text = rating.ToString();
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (Rating == 0)
            {
                MessageBox.Show("Пожалуйста, поставьте оценку.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FeedbackText = FeedbackTextBox.Text?.Trim() ?? string.Empty;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}