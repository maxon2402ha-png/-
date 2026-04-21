using System;
using System.Windows;

namespace КР_Ханников.Windows
{
    public partial class ResolutionViewWindow : Window
    {
        public ResolutionViewWindow()
        {
            InitializeComponent();
        }

        public ResolutionViewWindow(string resolutionText, string? resolvedBy = null,
            DateTime? resolvedAt = null, int? ticketId = null) : this()
        {
            if (ResolutionTextBlock != null)
                ResolutionTextBlock.Text = resolutionText;

            if (!string.IsNullOrEmpty(resolvedBy) && ResolvedByText != null)
                ResolvedByText.Text = resolvedBy;

            if (resolvedAt.HasValue && ResolvedAtText != null)
                ResolvedAtText.Text = resolvedAt.Value.ToString("dd.MM.yyyy HH:mm");

            if (ticketId.HasValue && TicketIdText != null)
                TicketIdText.Text = $"#{ticketId}";
        }

        private void CopyResolution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ResolutionTextBlock == null || string.IsNullOrEmpty(ResolutionTextBlock.Text))
                {
                    MessageBox.Show("Нет текста для копирования", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetText(ResolutionTextBlock.Text);

                MessageBox.Show("Текст решения скопирован в буфер обмена", "Скопировано",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось скопировать: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}