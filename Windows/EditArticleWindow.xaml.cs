using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    public partial class EditArticleWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly int _articleId;
        private KnowledgeArticle _article = null!;
        private bool _hasUnsavedChanges = false;
        private string _originalTitle = string.Empty;
        private string _originalContent = string.Empty;

        public EditArticleWindow(int articleId) : this(articleId, new AppDbContext()) { }

        public EditArticleWindow(int articleId, AppDbContext context)
        {
            InitializeComponent();
            _context = context ?? new AppDbContext();
            _articleId = articleId;

            LoadArticle();
            SetupEventHandlers();
        }

        private void LoadArticle()
        {
            try
            {
                var article = _context.KnowledgeBase.FirstOrDefault(a => a.Id == _articleId);

                if (article == null)
                {
                    MessageBox.Show("Статья не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                _article = article;

                // Заполняем поля
                TitleBox.Text = _article.Title ?? string.Empty;
                ContentBox.Text = _article.Content ?? string.Empty;

                if (ArticleIdText != null) ArticleIdText.Text = $"ID: {_article.Id}";

                // Сохраняем оригинальные значения
                _originalTitle = TitleBox.Text;
                _originalContent = ContentBox.Text;

                UpdateCharCounters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статьи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void SetupEventHandlers()
        {
            TitleBox.TextChanged += (s, e) => { UpdateCharCounters(); CheckForChanges(); };
            ContentBox.TextChanged += (s, e) => { UpdateCharCounters(); CheckForChanges(); };
        }

        private void UpdateCharCounters()
        {
            // Заголовок
            int titleLen = TitleBox.Text?.Length ?? 0;
            if (TitleCharCount != null)
            {
                TitleCharCount.Text = $"{titleLen} / 200";
                TitleCharCount.Foreground = titleLen > 180
                    ? System.Windows.Media.Brushes.Red
                    : System.Windows.Media.Brushes.Gray;
            }

            // Контент
            string content = ContentBox.Text ?? string.Empty;
            if (ContentCharCount != null) ContentCharCount.Text = $"{content.Length:N0} символов";

            if (ContentWordCount != null)
            {
                int words = string.IsNullOrWhiteSpace(content) ? 0 :
                    content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                ContentWordCount.Text = $"{words:N0} слов";
            }
        }

        private void CheckForChanges()
        {
            bool changed = TitleBox.Text != _originalTitle || ContentBox.Text != _originalContent;
            _hasUnsavedChanges = changed;
            if (UnsavedBadge != null)
                UnsavedBadge.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Обработчики кнопок форматирования ---

        private void Bold_Click(object sender, RoutedEventArgs e) => InsertMarkdown("**", "**");
        private void Italic_Click(object sender, RoutedEventArgs e) => InsertMarkdown("*", "*");

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewSection.Visibility == Visibility.Visible)
            {
                PreviewSection.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (ArticlePreviewText != null) ArticlePreviewText.Text = ContentBox.Text;
                PreviewSection.Visibility = Visibility.Visible;
            }
        }

        private void InsertMarkdown(string start, string end)
        {
            if (ContentBox == null) return;

            int selectionStart = ContentBox.SelectionStart;
            int selectionLength = ContentBox.SelectionLength;
            string text = ContentBox.Text;
            string selectedText = ContentBox.SelectedText;

            string newText = text.Substring(0, selectionStart) +
                             start + selectedText + end +
                             text.Substring(selectionStart + selectionLength);

            ContentBox.Text = newText;
            ContentBox.Focus();
            ContentBox.SelectionStart = selectionStart + start.Length;
            ContentBox.SelectionLength = selectionLength;
        }

        // --- Сохранение ---

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("Введите заголовок статьи", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(ContentBox.Text))
            {
                MessageBox.Show("Введите содержание статьи", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                ContentBox.Focus();
                return;
            }

            try
            {
                var articleToUpdate = _context.KnowledgeBase.FirstOrDefault(a => a.Id == _articleId);

                if (articleToUpdate != null)
                {
                    articleToUpdate.Title = TitleBox.Text.Trim();
                    articleToUpdate.Content = ContentBox.Text.Trim();
                    _context.SaveChanges();
                }

                _hasUnsavedChanges = false;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges && DialogResult != true)
            {
                var result = MessageBox.Show(
                    "Есть несохранённые изменения. Закрыть без сохранения?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }
    }
}