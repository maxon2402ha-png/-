using System.Windows;
using КР_Ханников.Core;

namespace КР_Ханников.Windows
{
    public partial class LinkExternalDialog : Window
    {
        private readonly int _ticketId;
        private readonly string _ticketTitle = string.Empty;

        public ExternalSystem? SelectedSystem { get; private set; }

        public LinkExternalDialog()
        {
            InitializeComponent();

            // В XAML по умолчанию уже стоит IsChecked="True" у JiraRadio,
            // но на всякий случай продублируем
            if (JiraRadio != null)
                JiraRadio.IsChecked = true;
        }

        // Конструктор с параметрами для вызова из TicketDetailsWindow
        public LinkExternalDialog(int ticketId, string ticketTitle) : this()
        {
            _ticketId = ticketId;
            _ticketTitle = ticketTitle ?? string.Empty;

            // Показываем информацию о тикете в заголовке диалога
            if (TicketInfoText != null)
            {
                TicketInfoText.Text = $"Тикет #{ticketId}: {ticketTitle}";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedSystem = null;
            DialogResult = false;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Определяем выбранную систему по RadioButton’ам
            if (JiraRadio != null && JiraRadio.IsChecked == true)
            {
                SelectedSystem = ExternalSystem.Jira;
            }
            else if (TrelloRadio != null && TrelloRadio.IsChecked == true)
            {
                SelectedSystem = ExternalSystem.Trello;
            }
            else if (BugzillaRadio != null && BugzillaRadio.IsChecked == true)
            {
                SelectedSystem = ExternalSystem.Bugzilla;
            }
            else if (MantisRadio != null && MantisRadio.IsChecked == true)
            {
                SelectedSystem = ExternalSystem.Mantis;
            }
            else
            {
                SelectedSystem = null;
            }

            DialogResult = SelectedSystem != null;
        }
    }
}