using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    public class TicketService
    {
        private readonly AppDbContext _context;

        public TicketService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // --- ЛОГИКА АВТОМАТИЗАЦИИ ---

        /// <summary>
        /// Выбирает наименее загруженного сотрудника (Support) для назначения.
        /// </summary>
        private async Task<int?> GetBestEmployeeForTicketAsync(CancellationToken ct)
        {
            // Ищем всех сотрудников с ролью Support
            var candidates = await _context.Employees
                .Include(e => e.User) // Чтобы проверить роль
                .Where(e => e.User.Role == Constants.UserRoles.Support)
                .ToListAsync(ct);

            if (!candidates.Any()) return null;

            // Считаем нагрузку: сколько у каждого открытых тикетов
            var bestCandidate = candidates
                .Select(emp => new
                {
                    Employee = emp,
                    // Считаем активные тикеты (не закрытые)
                    ActiveCount = _context.Tickets.Count(t => t.AssigneeEmployeeId == emp.Id && t.Status != Constants.TicketStatus.Closed)
                })
                .OrderBy(x => x.ActiveCount) // Сортируем по возрастанию нагрузки
                .FirstOrDefault();

            return bestCandidate?.Employee.Id;
        }

        /// <summary>
        /// Рассчитывает дедлайн на основе приоритета.
        /// </summary>
        private DateTime CalculateDeadline(TicketPriority priority)
        {
            var now = DateTime.UtcNow;
            return priority switch
            {
                TicketPriority.Critical => now.AddHours(4),   // 4 часа на критичный
                TicketPriority.High => now.AddHours(24),      // 1 сутки на высокий
                TicketPriority.Normal => now.AddDays(3),      // 3 дня на обычный
                TicketPriority.Low => now.AddDays(7),         // Неделя на низкий
                _ => now.AddDays(3)
            };
        }

        // --- CRUD ---

        public async Task<Ticket> CreateAsync(
            int clientId,
            string title,
            string description,
            DateTime? manualDueAt = null, // Если null, рассчитаем автоматически
            int? authorUserId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required", nameof(title));

            // 1. Авто-классификация (ML)
            var classifier = new TicketClassifier(new RuleBasedTicketClassifier());
            var (category, priority) = classifier.Classify(title, description ?? string.Empty);

            // 2. Авто-расчет дедлайна (если не задан вручную)
            var dueAt = manualDueAt ?? CalculateDeadline(priority);

            // 3. Авто-назначение сотрудника
            int? assigneeId = await GetBestEmployeeForTicketAsync(ct);
            string status = assigneeId.HasValue ? Constants.TicketStatus.InProgress : Constants.TicketStatus.Open;

            var ticket = new Ticket
            {
                ClientId = clientId,
                Title = title.Trim(),
                Description = description ?? string.Empty,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                DueAt = dueAt,
                UserId = authorUserId,
                Category = category,
                Priority = priority,
                AssigneeEmployeeId = assigneeId
            };

            await _context.Tickets.AddAsync(ticket, ct);

            // Сначала сохраняем тикет, чтобы получить ID
            await _context.SaveChangesAsync(ct);

            // 4. История: Запись о создании и авто-действиях
            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = ticket.Id,
                Action = "Создание (Auto)",
                Details = $"Авто-классификация: {category}/{priority}. Срок: {dueAt:dd.MM HH:mm}",
                Timestamp = DateTime.UtcNow
            }, ct);

            if (assigneeId.HasValue)
            {
                var empName = await _context.Employees.Where(e => e.Id == assigneeId).Select(e => e.Name).FirstOrDefaultAsync(ct);
                await _context.TicketHistories.AddAsync(new TicketHistory
                {
                    TicketId = ticket.Id,
                    Action = "Авто-назначение",
                    Details = $"Назначен наименее загруженный оператор: {empName}",
                    Timestamp = DateTime.UtcNow
                }, ct);
            }

            await _context.SaveChangesAsync(ct);
            return ticket;
        }

        public async Task<Ticket?> UpdateAsync(
            int ticketId,
            string? title = null,
            string? description = null,
            DateTime? dueAt = null,
            CancellationToken ct = default)
        {
            var t = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return null;

            var oldTitle = t.Title;
            var oldDesc = t.Description;
            var oldDue = t.DueAt;

            if (!string.IsNullOrWhiteSpace(title)) t.Title = title.Trim();
            if (description != null) t.Description = description;

            // Разрешаем менять дедлайн вручную
            if (dueAt.HasValue) t.DueAt = dueAt;

            t.UpdatedAt = DateTime.UtcNow;

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = t.Id,
                Action = "Обновление",
                Details = BuildUpdateDetails(oldTitle, t.Title, oldDesc, t.Description, oldDue, t.DueAt),
                Timestamp = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            return t;
        }

        public async Task<bool> DeleteAsync(int ticketId, CancellationToken ct = default)
        {
            var t = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return false;

            _context.Tickets.Remove(t);
            // Историю можно не писать при физическом удалении, или использовать Soft Delete
            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> AssignAsync(int ticketId, int employeeId, CancellationToken ct = default)
        {
            var t = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return false;

            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
            if (emp == null) throw new InvalidOperationException("Сотрудник не найден");

            var oldStatus = t.Status;
            t.AssigneeEmployeeId = employeeId;

            if (t.Status == Constants.TicketStatus.Open)
                t.Status = Constants.TicketStatus.InProgress;

            t.UpdatedAt = DateTime.UtcNow;

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = t.Id,
                Action = "Назначение",
                Details = $"Назначен сотрудник: {emp.Name} (ID={emp.Id})",
                Timestamp = DateTime.UtcNow
            }, ct);

            if (oldStatus != t.Status)
            {
                await _context.TicketHistories.AddAsync(new TicketHistory
                {
                    TicketId = t.Id,
                    Action = "Статус",
                    Details = $"{oldStatus} -> {t.Status}",
                    Timestamp = DateTime.UtcNow
                }, ct);
            }

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ChangeStatusAsync(int ticketId, string newStatus, CancellationToken ct = default)
        {
            if (!Ticket.AllStatuses.Contains(newStatus))
                throw new ArgumentException($"Недопустимый статус: {newStatus}", nameof(newStatus));

            var t = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return false;

            var oldStatus = t.Status;
            if (oldStatus == newStatus) return true;

            t.Status = newStatus;
            t.UpdatedAt = DateTime.UtcNow;

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = t.Id,
                Action = "Статус",
                Details = $"{oldStatus} -> {newStatus}",
                Timestamp = DateTime.UtcNow
            }, ct);

            if (newStatus == Constants.TicketStatus.Closed)
                t.ClosedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> SetDueDateAsync(int ticketId, DateTime? dueAt, CancellationToken ct = default)
        {
            var t = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return false;

            var old = t.DueAt;
            t.DueAt = dueAt;
            t.UpdatedAt = DateTime.UtcNow;

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = t.Id,
                Action = "Дедлайн",
                Details = BuildDueDetails(old, dueAt),
                Timestamp = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            return true;
        }

     
        public async Task<TicketComment> AddCommentAsync(
            int ticketId,
            int userId,
            string text,
            bool isInternal = true,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Комментарий не может быть пустым", nameof(text));

            var comment = new TicketComment
            {
                TicketId = ticketId,
                UserId = userId,
                Text = text.Trim(),
                IsInternal = isInternal,
                CreatedAt = DateTime.UtcNow
            };

            await _context.TicketComments.AddAsync(comment, ct);

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = ticketId,
                Action = isInternal ? "[Internal] Комментарий" : "Комментарий",
                Details = text.Trim(),
                Timestamp = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            return comment;
        }

        public async Task<bool> CloseAsync(
            int ticketId,
            string resolutionText,
            int resolvedByEmployeeId,
            CancellationToken ct = default)
        {
            var t = await _context.Tickets.Include(x => x.Solution).FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t == null) return false;

            await ChangeStatusAsync(ticketId, Constants.TicketStatus.Closed, ct);

            if (t.Solution == null)
            {
                t.Solution = new Solution
                {
                    TicketId = t.Id,
                    ResolutionText = resolutionText.Trim(),
                    ResolutionDate = DateTime.UtcNow
                };
                await _context.Solutions.AddAsync(t.Solution, ct);
            }
            else
            {
                t.Solution.ResolutionText = resolutionText.Trim();
                t.Solution.ResolutionDate = DateTime.UtcNow;
            }

            t.UpdatedAt = DateTime.UtcNow;
            t.ClosedAt = DateTime.UtcNow;

            await _context.TicketHistories.AddAsync(new TicketHistory
            {
                TicketId = t.Id,
                Action = "Закрытие",
                Details = $"Тикет закрыт сотрудником #{resolvedByEmployeeId}",
                Timestamp = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            return true;
        }

        private static string BuildUpdateDetails(string oldTitle, string newTitle, string oldDesc, string newDesc, DateTime? oldDue, DateTime? newDue)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.Equals(oldTitle, newTitle, StringComparison.Ordinal)) parts.Add($"Заголовок изменен");
            if (!string.Equals(oldDesc, newDesc, StringComparison.Ordinal)) parts.Add("Описание изменено");
            if (oldDue != newDue) parts.Add($"Срок: {FormatDate(oldDue)} -> {FormatDate(newDue)}");
            return parts.Count == 0 ? "Изменений нет" : string.Join("; ", parts);
        }

        private static string BuildDueDetails(DateTime? oldDue, DateTime? newDue) => $"Срок: {FormatDate(oldDue)} -> {FormatDate(newDue)}";
        private static string FormatDate(DateTime? dt) => dt.HasValue ? dt.Value.ToString("dd.MM.yyyy HH:mm") : "не задан";
    }
}