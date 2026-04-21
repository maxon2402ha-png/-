using System.Windows;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    public partial class AddArticleWindow : Window
    {
        private readonly AppDbContext _context;

        public AddArticleWindow(AppDbContext context)
        {
            InitializeComponent();
            _context = context;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(ContentBox.Text))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _context.KnowledgeBase.Add(new KnowledgeArticle
            {
                Title = TitleBox.Text,
                Content = ContentBox.Text
            });

            _context.SaveChanges();

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