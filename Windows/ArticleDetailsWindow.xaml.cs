using System.Runtime.Versioning;
using System.Windows;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class ArticleDetailsWindow : Window
    {
        public ArticleDetailsWindow(KnowledgeArticle article)
        {
            InitializeComponent();
            DataContext = article;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}