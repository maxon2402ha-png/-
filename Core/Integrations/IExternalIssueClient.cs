using System.Threading;
using System.Threading.Tasks;
using КР_Ханников.Core;

namespace КР_Ханников.Services
{
    public interface IExternalIssueClient
    {
        ExternalSystem System { get; }

        Task<CreateIssueResult> CreateAsync(Ticket ticket, IntegrationSettings settings, CancellationToken ct);
        Task<UpdateIssueResult> UpdateAsync(Ticket ticket, ExternalLink link, IntegrationSettings settings, CancellationToken ct);
        Task<ExternalIssue?> GetAsync(ExternalLink link, IntegrationSettings settings, CancellationToken ct);
        Task<ExternalIssue?> FindByKeyAsync(string externalKey, IntegrationSettings settings, CancellationToken ct);
    }

    public record CreateIssueResult(string ExternalId, string? ExternalKey, string? Url);
    public record UpdateIssueResult(bool Success);

    public class ExternalIssue
    {
        public string ExternalId { get; init; } = "";
        public string? ExternalKey { get; init; }
        public string? Url { get; init; }

        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public string Status { get; init; } = "";
        public string? Priority { get; init; }
        public string? Assignee { get; init; }
        public System.DateTime? Due { get; init; }
    }
}