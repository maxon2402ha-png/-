using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using КР_Ханников.Core;

namespace КР_Ханников.Services
{
    [SupportedOSPlatform("windows")]
    public class TrelloClient : IExternalIssueClient
    {
        private readonly HttpClient _http;

        public TrelloClient(HttpClient http)
        {
            _http = http;
        }

        public ExternalSystem System => ExternalSystem.Trello;

        private string BuildAuth(IntegrationSettings s)
        {
            var key = s.AuthLogin ?? string.Empty;
            var tokenPlain = CryptoHelper.DecryptSensitive(s.AuthSecret) ?? s.AuthSecret ?? string.Empty;
            return $"key={Uri.EscapeDataString(key)}&token={Uri.EscapeDataString(tokenPlain)}";
        }

        public async Task<CreateIssueResult> CreateAsync(Ticket t, IntegrationSettings s, CancellationToken ct)
        {
            var url = $"https://api.trello.com/1/cards?{BuildAuth(s)}&idList={s.BoardOrListId}&name={Uri.EscapeDataString(t.Title)}&desc={Uri.EscapeDataString(IssueMapper.BuildDescription(t))}";

            var resp = await _http.PostAsync(url, null, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var id = doc.RootElement.GetProperty("id").GetString()!;
            var shortUrl = doc.RootElement.GetProperty("shortUrl").GetString();

            return new CreateIssueResult(id, null, shortUrl);
        }

        public async Task<UpdateIssueResult> UpdateAsync(Ticket t, ExternalLink link, IntegrationSettings s, CancellationToken ct)
        {
            // Обновляем имя и описание
            var url = $"https://api.trello.com/1/cards/{link.ExternalId}?{BuildAuth(s)}&name={Uri.EscapeDataString(t.Title)}&desc={Uri.EscapeDataString(IssueMapper.BuildDescription(t))}";

            var resp = await _http.PutAsync(url, null, ct);
            return new UpdateIssueResult(resp.IsSuccessStatusCode);
        }

        public async Task<ExternalIssue?> GetAsync(ExternalLink link, IntegrationSettings s, CancellationToken ct)
        {
            var url = $"https://api.trello.com/1/cards/{link.ExternalId}?{BuildAuth(s)}";

            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            bool closed = doc.RootElement.GetProperty("closed").GetBoolean();

            return new ExternalIssue
            {
                ExternalId = doc.RootElement.GetProperty("id").GetString()!,
                Title = doc.RootElement.GetProperty("name").GetString()!,
                // В Trello статус определяется флагом closed (архивировано) или списком. 
                // Упрощенно считаем: closed = Closed, иначе Open.
                Status = closed ? "Closed" : "Open"
            };
        }

        public Task<ExternalIssue?> FindByKeyAsync(string key, IntegrationSettings s, CancellationToken ct) => Task.FromResult<ExternalIssue?>(null);
    }
}