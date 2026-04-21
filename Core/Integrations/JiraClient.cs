using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using КР_Ханников.Core;

namespace КР_Ханников.Services
{
    [SupportedOSPlatform("windows")]
    public class JiraClient : IExternalIssueClient
    {
        private readonly HttpClient _http;

        public JiraClient(HttpClient httpClient)
        {
            _http = httpClient;
        }

        public ExternalSystem System => ExternalSystem.Jira;

        private void ApplyAuth(IntegrationSettings s)
        {
            var login = s.AuthLogin ?? string.Empty;
            var secretPlain = CryptoHelper.DecryptSensitive(s.AuthSecret) ?? s.AuthSecret ?? string.Empty;
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{secretPlain}"));

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<CreateIssueResult> CreateAsync(Ticket t, IntegrationSettings s, CancellationToken ct)
        {
            ApplyAuth(s);
            var url = $"{s.BaseUrl.TrimEnd('/')}/rest/api/3/issue";

            var payload = new
            {
                fields = new
                {
                    project = new { key = s.ProjectKey },
                    summary = t.Title,
                    description = new // ADF format (упрощенно)
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new {
                                type = "paragraph",
                                content = new[] { new { type = "text", text = IssueMapper.BuildDescription(t) } }
                            }
                        }
                    },
                    issuetype = new { name = s.DefaultIssueType },
                    priority = new { name = IssueMapper.ToJiraPriority(t.Priority) }
                }
            };

            var body = JsonSerializer.Serialize(payload);
            var resp = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync(ct);
                throw new Exception($"Jira Error: {resp.StatusCode} - {error}");
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var doc = JsonSerializer.Deserialize<JiraIssueResponse>(json)!;

            var browseUrl = $"{s.BaseUrl.TrimEnd('/')}/browse/{doc.key}";
            return new CreateIssueResult(doc.id, doc.key, browseUrl);
        }

        public async Task<UpdateIssueResult> UpdateAsync(Ticket t, ExternalLink link, IntegrationSettings s, CancellationToken ct)
        {
            ApplyAuth(s);
            var url = $"{s.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{link.ExternalKey}";

            var payload = new
            {
                fields = new
                {
                    summary = t.Title,
                    priority = new { name = IssueMapper.ToJiraPriority(t.Priority) }
                    // Description в Jira обновлять сложнее из-за формата ADF, опускаем для простоты
                }
            };

            var body = JsonSerializer.Serialize(payload);
            var resp = await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);

            return new UpdateIssueResult(resp.IsSuccessStatusCode);
        }

        public async Task<ExternalIssue?> GetAsync(ExternalLink link, IntegrationSettings s, CancellationToken ct)
        {
            ApplyAuth(s);
            var url = $"{s.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{link.ExternalKey}?fields=status,priority,assignee";

            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var fields = doc.RootElement.GetProperty("fields");

            return new ExternalIssue
            {
                ExternalId = doc.RootElement.GetProperty("id").GetString()!,
                ExternalKey = doc.RootElement.GetProperty("key").GetString(),
                Status = fields.GetProperty("status").GetProperty("name").GetString() ?? "",
                Priority = fields.TryGetProperty("priority", out var p) ? p.GetProperty("name").GetString() : null
            };
        }

        public Task<ExternalIssue?> FindByKeyAsync(string externalKey, IntegrationSettings s, CancellationToken ct) => Task.FromResult<ExternalIssue?>(null);

        private record JiraIssueResponse(string id, string key, string self);
    }
}