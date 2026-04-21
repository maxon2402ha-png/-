using System;
using System.Runtime.Versioning;
using System.Windows;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class ArticleEditorWindow : Window
    {
        private readonly KnowledgeArticle? _article;

        public string ArticleTitle { get; private set; } = string.Empty;
        public string ArticleContent { get; private set; } = string.Empty;

        // Конструктор для создания
        public ArticleEditorWindow()
        {
            InitializeComponent();
            WindowTitle.Text = "Новая статья";
            TitleBox.Focus();
        }

        // Конструктор для редактирования
        public ArticleEditorWindow(KnowledgeArticle article)
        {
            InitializeComponent();
            _article = article;
            WindowTitle.Text = "Редактирование статьи";

            TitleBox.Text = article.Title;
            ContentBox.Text = article.Content;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(ContentBox.Text))
            {
                MessageBox.Show("Заполните заголовок и содержание.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ArticleTitle = TitleBox.Text.Trim();
            ArticleContent = ContentBox.Text.Trim();

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