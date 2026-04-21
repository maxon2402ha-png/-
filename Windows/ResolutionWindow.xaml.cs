using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.Versioning;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class ResolutionWindow : Window
    {
        private readonly int _ticketId;
        private List<KnowledgeArticle> _articles = new();

        public string ResolutionText => ResolutionTextBox?.Text ?? string.Empty;

        public ResolutionWindow(int ticketId, string ticketTitle)
        {
            InitializeComponent();
            _ticketId = ticketId;

            if (TicketIdText != null) TicketIdText.Text = $"#{ticketId}";
            if (TicketTitleText != null) TicketTitleText.Text = ticketTitle;

            LoadKnowledgeBase();

            // Фокус на поле ввода при загрузке
            Loaded += (s, e) => ResolutionTextBox.Focus();
        }

        private void LoadKnowledgeBase()
        {
            try
            {
                using var db = new AppDbContext();

                _articles = db.KnowledgeBase
                    .OrderBy(a => a.Title)
                    .ToList();

                if (KnowledgeBaseComboBox != null)
                {
                    KnowledgeBaseComboBox.ItemsSource = _articles;
                    KnowledgeBaseComboBox.DisplayMemberPath = "Title";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки базы знаний: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KnowledgeBaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KnowledgeBaseComboBox?.SelectedItem is KnowledgeArticle article)
            {
                if (ArticlePreviewText != null)
                {
                    ArticlePreviewText.Text = article.Content;
                    ArticlePreviewText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#374151"));
                }
                if (UseTemplateButton != null)
                    UseTemplateButton.IsEnabled = true;
            }
            else
            {
                if (ArticlePreviewText != null)
                {
                    ArticlePreviewText.Text = "Выберите статью для просмотра...";
                    ArticlePreviewText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#9CA3AF"));
                }
                if (UseTemplateButton != null)
                    UseTemplateButton.IsEnabled = false;
            }
        }

        private void UseTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (KnowledgeBaseComboBox?.SelectedItem is KnowledgeArticle article && ResolutionTextBox != null)
            {
                string textToInsert = article.Content;

                if (!string.IsNullOrWhiteSpace(ResolutionTextBox.Text))
                {
                    var result = MessageBox.Show(
                        "Заменить текущий текст (Yes) или добавить в конец (No)?\nCancel - отмена действия.",
                        "Вставка шаблона",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        ResolutionTextBox.Text = textToInsert;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        ResolutionTextBox.Text += "\n\n" + textToInsert;
                    }
                    else
                    {
                        return; // Отмена
                    }
                }
                else
                {
                    ResolutionTextBox.Text = textToInsert;
                }

                ResolutionTextBox.Focus();
                ResolutionTextBox.CaretIndex = ResolutionTextBox.Text.Length;
            }
        }

        private void ResolutionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int length = ResolutionTextBox?.Text?.Length ?? 0;
            if (CharCountText != null) CharCountText.Text = $"{length} символов";
            if (SaveButton != null) SaveButton.IsEnabled = length > 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ResolutionTextBox?.Text))
            {
                MessageBox.Show("Введите текст решения", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ResolutionTextBox?.Focus();
                return;
            }

            if (ResolutionTextBox.Text.Length < 10)
            {
                MessageBox.Show("Текст решения слишком короткий (минимум 10 символов)", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ResolutionTextBox.Focus();
                return;
            }

            var result = MessageBox.Show(
                "После сохранения тикет будет закрыт.\nПродолжить?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ResolutionTextBox?.Text))
            {
                var result = MessageBox.Show(
                    "Введённый текст будет потерян.\nВы уверены?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            DialogResult = false;
            Close();
        }
    }
}