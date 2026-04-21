using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    public class IntegrationService
    {
        private readonly AppDbContext _db;
        private readonly IExternalIssueClient[] _clients;

        public IntegrationService(AppDbContext db, params IExternalIssueClient[] clients)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _clients = clients ?? Array.Empty<IExternalIssueClient>();
        }

        private IExternalIssueClient? GetClient(ExternalSystem system)
            => _clients.FirstOrDefault(c => c.System == system);

        /// <summary>
        /// Создает задачу во внешней системе и сохраняет связь.
        /// </summary>
        public async Task<ExternalLink> LinkAndPushAsync(int ticketId, ExternalSystem system, CancellationToken ct)
        {
            var ticket = await _db.Tickets
                .Include(t => t.Assignee).ThenInclude(a => a != null ? a.User : null)
                .FirstAsync(t => t.Id == ticketId, ct);

            var settings = await _db.IntegrationSettings
                .FirstOrDefaultAsync(s => s.System == system, ct)
                ?? throw new InvalidOperationException($"Не найдены настройки для {system}");

            var client = GetClient(system)
                ?? throw new InvalidOperationException($"Клиент для {system} не зарегистрирован");

            var created = await client.CreateAsync(ticket, settings, ct);
            if (created == null)
                throw new InvalidOperationException("Внешняя система вернула пустой результат");

            var link = new ExternalLink
            {
                TicketId = ticket.Id,
                System = system,
                ExternalId = created.ExternalId ?? string.Empty,
                ExternalKey = created.ExternalKey ?? string.Empty,
                Url = created.Url ?? string.Empty,
                LastSyncedAt = DateTime.UtcNow,
                ContentHash = IssueMapper.ComputeContentHash(ticket) ?? string.Empty
            };

            _db.ExternalLinks.Add(link);
            await _db.SaveChangesAsync(ct);
            return link;
        }

        /// <summary>
        /// Отправляет локальные изменения во внешнюю систему.
        /// </summary>
        public async Task PushAsync(int ticketId, ExternalSystem system, CancellationToken ct)
        {
            var ticket = await _db.Tickets
                .Include(t => t.Assignee).ThenInclude(a => a != null ? a.User : null)
                .FirstAsync(t => t.Id == ticketId, ct);

            var link = await _db.ExternalLinks
                .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.System == system, ct)
                ?? throw new InvalidOperationException("Связь не найдена");

            var settings = await _db.IntegrationSettings
                .FirstOrDefaultAsync(s => s.System == system, ct)
                ?? throw new InvalidOperationException($"Нет настроек для {system}");

            var client = GetClient(system);
            if (client != null)
            {
                await client.UpdateAsync(ticket, link, settings, ct);

                link.LastSyncedAt = DateTime.UtcNow;
                link.ContentHash = IssueMapper.ComputeContentHash(ticket);
                await _db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Забирает изменения из внешней системы (синхронизация статусов).
        /// </summary>
        public async Task PullAsync(int ticketId, ExternalSystem system, CancellationToken ct)
        {
            var ticket = await _db.Tickets.FirstAsync(t => t.Id == ticketId, ct);
            var link = await _db.ExternalLinks.FirstOrDefaultAsync(x => x.TicketId == ticketId && x.System == system, ct);

            if (link == null) return;

            var settings = await _db.IntegrationSettings.FirstOrDefaultAsync(s => s.System == system, ct);
            if (settings == null) return;

            var client = GetClient(system);
            if (client == null) return;

            // Получаем данные из внешней системы
            var externalIssue = await client.GetAsync(link, settings, ct);

            if (externalIssue != null)
            {
                bool changed = false;

                // Пример синхронизации статуса (упрощенно)
                // Если в Jira статус "Done", а у нас нет — закрываем тикет
                if (externalIssue.Status.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                    externalIssue.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                {
                    if (ticket.Status != Constants.TicketStatus.Closed)
                    {
                        ticket.Status = Constants.TicketStatus.Closed;
                        ticket.ClosedAt = DateTime.UtcNow;
                        changed = true;

                        _db.TicketHistories.Add(new TicketHistory
                        {
                            TicketId = ticket.Id,
                            Action = "Авто-синхронизация",
                            Details = $"{system}: Статус изменен на Closed",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                // Если в Jira "In Progress"
                else if (externalIssue.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase))
                {
                    if (ticket.Status == Constants.TicketStatus.Open)
                    {
                        ticket.Status = Constants.TicketStatus.InProgress;
                        changed = true;
                    }
                }

                if (changed)
                {
                    link.LastSyncedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        /// <summary>
        /// Метод для умной синхронизации (проверяет, где данные новее).
        /// </summary>
        public async Task SyncIfChangedAsync(int ticketId, ExternalSystem system, CancellationToken ct)
        {
            // 1. Сначала пробуем подтянуть изменения (Pull)
            await PullAsync(ticketId, system, ct);

            // 2. Затем проверяем, не изменился ли локальный тикет, чтобы отправить (Push)
            var ticket = await _db.Tickets
                .Include(t => t.Assignee).ThenInclude(a => a != null ? a.User : null)
                .FirstAsync(t => t.Id == ticketId, ct);

            var link = await _db.ExternalLinks
                .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.System == system, ct);

            if (link == null)
            {
                await LinkAndPushAsync(ticketId, system, ct);
                return;
            }

            var currHash = IssueMapper.ComputeContentHash(ticket) ?? string.Empty;
            var lastHash = link.ContentHash ?? string.Empty;

            if (!string.Equals(currHash, lastHash, StringComparison.Ordinal))
            {
                await PushAsync(ticketId, system, ct);
            }
        }

        /// <summary>
        /// Массовая фоновая синхронизация всех активных связей.
        /// </summary>
        public async Task SyncAllActiveLinksAsync(CancellationToken ct)
        {
            var links = await _db.ExternalLinks.ToListAsync(ct);
            foreach (var link in links)
            {
                try
                {
                    // Синхронизируем каждый тикет
                    await SyncIfChangedAsync(link.TicketId, link.System, ct);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Sync] Ошибка синхронизации тикета #{link.TicketId}: {ex.Message}");
                }
            }
        }
    }
}