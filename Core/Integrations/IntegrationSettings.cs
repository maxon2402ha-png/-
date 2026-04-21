namespace КР_Ханников.Core
{
    /// <summary> Настройки интеграций (храним одно значение на систему). </summary>
    public class IntegrationSettings
    {
        public int Id { get; set; }
        public ExternalSystem System { get; set; }

        // Общие поля
        public string BaseUrl { get; set; } = "";    // напр. "https://your-domain.atlassian.net"
        public string ProjectKey { get; set; } = ""; // Jira/Mantis/Bugzilla
        public string BoardOrListId { get; set; } = ""; // Trello: id списка

        // Аутентификация
        public string AuthLogin { get; set; } = "";  // Jira: e-mail
        public string AuthSecret { get; set; } = ""; // Jira: API token ; Trello: token; Bugzilla/Mantis: токен/пароль

        // Доп. параметры маппинга
        public string DefaultIssueType { get; set; } = "Task"; // Jira
        public string DefaultPriority { get; set; } = "Medium";
    }
}
