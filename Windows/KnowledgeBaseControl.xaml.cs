using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using КР_Ханников.Core;
using КР_Ханников.Data;
using КР_Ханников.Services;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class KnowledgeBaseControl : UserControl
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private List<KnowledgeArticle> _allArticles = new();

        public KnowledgeBaseControl(AppDbContext context, AuthService authService)
        {
            InitializeComponent();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            DataContext = this;

            ConfigureAccess();
            _ = LoadArticlesAsync();
        }

        public User? CurrentUser => _authService.CurrentUser;

        private void ConfigureAccess()
        {
            // Ограничиваем только Клиентам (Саппорт и Админ могут видеть кнопки)
            if (_authService.CurrentUser?.Role == Constants.UserRoles.Client)
            {
                if (AddArticleButton != null) AddArticleButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadArticlesAsync()
        {
            try
            {
                _allArticles = await _context.KnowledgeBase.AsNoTracking().ToListAsync();
                UpdateGridSource(_allArticles);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статей: {ex.Message}");
            }
        }

        private void UpdateGridSource(IEnumerable<KnowledgeArticle> articles)
        {
            if (ArticlesGrid == null) return;
            var list = articles.ToList();
            ArticlesGrid.ItemsSource = list;
            if (ArticleCountText != null) ArticleCountText.Text = list.Count.ToString();
            if (EmptyState != null) EmptyState.Visibility = list.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(query))
            {
                UpdateGridSource(_allArticles);
            }
            else
            {
                var filtered = _allArticles.Where(a =>
                    (a.Title != null && a.Title.ToLower().Contains(query)) ||
                    (a.Content != null && a.Content.ToLower().Contains(query))
                );
                UpdateGridSource(filtered);
            }
        }

        private async void AddArticle_Click(object sender, RoutedEventArgs e)
        {
            var editor = new ArticleEditorWindow { Owner = Window.GetWindow(this) };
            if (editor.ShowDialog() == true)
            {
                try
                {
                    var article = new KnowledgeArticle
                    {
                        Title = editor.ArticleTitle,
                        Content = editor.ArticleContent,
                        AuthorId = _authService.CurrentUser!.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.KnowledgeBase.Add(article);
                    await _context.SaveChangesAsync();
                    await LoadArticlesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        private async void EditArticle_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.CurrentUser?.Role == Constants.UserRoles.Client)
            {
                MessageBox.Show("Нет прав."); return;
            }

            if (sender is FrameworkElement element && element.DataContext is KnowledgeArticle article)
            {
                // Используем ArticleEditorWindow для редактирования
                var editor = new ArticleEditorWindow(article) { Owner = Window.GetWindow(this) };
                if (editor.ShowDialog() == true)
                {
                    try
                    {
                        var item = _context.KnowledgeBase.FirstOrDefault(k => k.Id == article.Id);
                        if (item != null)
                        {
                            item.Title = editor.ArticleTitle;
                            item.Content = editor.ArticleContent;
                            item.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                            await LoadArticlesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка обновления: {ex.Message}");
                    }
                }
            }
        }

        private async void DeleteArticle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is KnowledgeArticle article)
            {
                if (MessageBox.Show($"Удалить \"{article.Title}\"?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        var item = _context.KnowledgeBase.FirstOrDefault(k => k.Id == article.Id);
                        if (item != null)
                        {
                            _context.KnowledgeBase.Remove(item);
                            await _context.SaveChangesAsync();
                            await LoadArticlesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox != null) SearchBox.Text = string.Empty;
            await LoadArticlesAsync();
        }

        private void ArticlesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ArticlesGrid?.SelectedItem is KnowledgeArticle article)
            {
                // Открываем окно просмотра (ArticleDetailsWindow)
                new ArticleDetailsWindow(article) { Owner = Window.GetWindow(this) }.ShowDialog();
            }
        }
    }
}