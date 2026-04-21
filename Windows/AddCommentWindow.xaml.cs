using System;
using System.Windows;

namespace КР_Ханников.Windows
{
    public partial class AddCommentWindow : Window
    {
        public string CommentText { get; private set; } = string.Empty;

        public AddCommentWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => CommentTextBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var text = CommentTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Комментарий пуст.");
                CommentTextBox.Focus();
                return;
            }

            if (text.Length > 5000)
            {
                MessageBox.Show("Слишком длинный комментарий.");
                return;
            }

            CommentText = text;
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