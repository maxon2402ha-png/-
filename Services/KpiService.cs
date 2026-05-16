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
                                                        public class KpiService
    {
        private readonly AppDbContext _context;

                                                public const int MaxWorkloadPoints = 20;

        public KpiService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

                private static readonly string[] ClosedLikeStatuses =
        {
            Constants.TicketStatus.Closed,
            Constants.TicketStatus.Resolved
        };

                                
                                                        public async Task<int> CalculateEmployeeWorkloadAsync(
            int employeeId,
            CancellationToken ct = default)
        {
            var activeTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AssigneeEmployeeId == employeeId
                         && t.Status != Constants.TicketStatus.Closed
                         && t.Status != Constants.TicketStatus.Resolved)
                .Select(t => t.Priority)
                .ToListAsync(ct);

            int load = activeTickets.Sum(p => WorkloadService.GetTicketWeight(p));

                        return Math.Min(load, MaxWorkloadPoints);
        }

                                                        public async Task<double> CalculateSlaComplianceAsync(
            int employeeId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            var resolved = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AssigneeEmployeeId == employeeId
                         && ClosedLikeStatuses.Contains(t.Status)
                         && t.ClosedAt != null
                         && t.DueAt != null
                         && t.ClosedAt >= fromUtc && t.ClosedAt <= toUtc)
                .Select(t => new { t.ClosedAt, t.DueAt })
                .ToListAsync(ct);

            if (resolved.Count == 0)
                return 100.0;

            int met = resolved.Count(t => t.ClosedAt!.Value <= t.DueAt!.Value);
            return Math.Round((double)met / resolved.Count * 100.0, 1);
        }

                                                public async Task<double> CalculateArtAsync(
            int employeeId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            var resolved = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AssigneeEmployeeId == employeeId
                         && ClosedLikeStatuses.Contains(t.Status)
                         && t.ClosedAt != null
                         && t.ClosedAt >= fromUtc && t.ClosedAt <= toUtc)
                .Select(t => new { t.ClosedAt, t.CreatedAt })
                .ToListAsync(ct);

            if (resolved.Count == 0)
                return 0.0;

            var hours = resolved
                .Select(t => (t.ClosedAt!.Value - t.CreatedAt).TotalHours)
                .Where(h => h >= 0)
                .ToList();

            return hours.Count > 0
                ? Math.Round(hours.Average(), 1)
                : 0.0;
        }

                        
                                        public async Task<KpiSummary> GetSummaryAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            var tickets = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.History)
                .Include(t => t.Comments)
                .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt <= toUtc)
                .ToListAsync(ct);

            return BuildSummary(tickets);
        }

                                        public async Task<List<EmployeeKpi>> GetEmployeeKpisAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.User)
                .Where(e => e.User.Role == Constants.UserRoles.Support
                         || e.User.Role == Constants.UserRoles.Admin)
                .ToListAsync(ct);

            var tickets = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.History)
                .Include(t => t.Comments)
                .Include(t => t.Feedback)
                .Where(t => t.AssigneeEmployeeId != null
                         && t.CreatedAt >= fromUtc && t.CreatedAt <= toUtc)
                .ToListAsync(ct);

            var result = new List<EmployeeKpi>();

            foreach (var emp in employees)
            {
                var empTickets = tickets
                    .Where(t => t.AssigneeEmployeeId == emp.Id)
                    .ToList();

                var summary = BuildSummary(empTickets);

                var ratings = empTickets
                    .Where(t => t.Feedback != null)
                    .Select(t => t.Feedback!.Rating)
                    .ToList();

                result.Add(new EmployeeKpi
                {
                    EmployeeId = emp.Id,
                    Name = emp.Name,
                    Username = emp.User?.Username ?? "—",
                    TotalTickets = summary.TotalTickets,
                    ResolvedTickets = summary.ResolvedTickets,
                    SlaCompliancePercent = summary.SlaCompliancePercent,
                    AvgFirstResponseHours = summary.AvgFirstResponseHours,
                    AvgResolutionHours = summary.AvgResolutionHours,
                    OverdueRate = summary.OverdueRate,
                    ReopenRate = summary.ReopenRate,
                    FeedbackCount = ratings.Count,
                    AvgRating = ratings.Count > 0 ? ratings.Average() : 0.0
                });
            }

            return result
                .OrderByDescending(k => k.SlaCompliancePercent)
                .ThenByDescending(k => k.ResolvedTickets)
                .ToList();
        }

                        
                                                private static KpiSummary BuildSummary(List<Ticket> tickets)
        {
            int total = tickets.Count;

            var resolved = tickets
                .Where(t => ClosedLikeStatuses.Contains(t.Status))
                .ToList();

            int resolvedCount = resolved.Count;

                        var slaTracked = resolved
                .Where(t => t.DueAt.HasValue && t.ClosedAt.HasValue)
                .ToList();

            int slaMet = slaTracked.Count(t => t.ClosedAt!.Value <= t.DueAt!.Value);

            double slaPercent = slaTracked.Count > 0
                ? (double)slaMet / slaTracked.Count * 100.0
                : 0.0;

                        var resolutionHours = resolved
                .Where(t => t.ClosedAt.HasValue)
                .Select(t => (t.ClosedAt!.Value - t.CreatedAt).TotalHours)
                .Where(h => h >= 0)
                .ToList();

            double avgResolution = resolutionHours.Count > 0
                ? resolutionHours.Average()
                : 0.0;

                        var frtHours = tickets
                .Select(GetFirstResponseHours)
                .Where(h => h.HasValue)
                .Select(h => h!.Value)
                .ToList();

            double avgFrt = frtHours.Count > 0
                ? frtHours.Average()
                : 0.0;

                        int overdue = tickets.Count(t => t.IsOverdue);
            double overdueRate = total > 0
                ? (double)overdue / total * 100.0
                : 0.0;

                        int reopened = tickets.Count(WasReopened);
            double reopenRate = total > 0
                ? (double)reopened / total * 100.0
                : 0.0;

            return new KpiSummary
            {
                TotalTickets = total,
                ResolvedTickets = resolvedCount,
                OpenTickets = tickets.Count(t => t.Status == Constants.TicketStatus.Open),
                InProgressTickets = tickets.Count(t => t.Status == Constants.TicketStatus.InProgress),
                OverdueTickets = overdue,
                SlaCompliancePercent = Math.Round(slaPercent, 1),
                AvgResolutionHours = Math.Round(avgResolution, 1),
                AvgFirstResponseHours = Math.Round(avgFrt, 1),
                OverdueRate = Math.Round(overdueRate, 1),
                ReopenRate = Math.Round(reopenRate, 1)
            };
        }

                                                        private static double? GetFirstResponseHours(Ticket ticket)
        {
            if (ticket.Comments == null || ticket.Comments.Count == 0)
                return null;

            var firstExternal = ticket.Comments
                .Where(c => !c.IsInternal && c.CreatedAt > ticket.CreatedAt)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefault();

            if (firstExternal == null)
                return null;

            double hours = (firstExternal.CreatedAt - ticket.CreatedAt).TotalHours;
            return hours >= 0 ? hours : null;
        }

                                                private static bool WasReopened(Ticket ticket)
        {
            if (ticket.History == null)
                return false;

            foreach (var h in ticket.History.Where(h => h.Action == "Статус"))
            {
                var parts = h.Details.Split("->", StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                    continue;

                bool fromClosed = ClosedLikeStatuses.Contains(parts[0]);
                bool toActive = parts[1] == Constants.TicketStatus.Open
                             || parts[1] == Constants.TicketStatus.InProgress;

                if (fromClosed && toActive)
                    return true;
            }

            return false;
        }
    }

                public class KpiSummary
    {
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int OpenTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int OverdueTickets { get; set; }

                public double SlaCompliancePercent { get; set; }

                public double AvgResolutionHours { get; set; }

                public double AvgFirstResponseHours { get; set; }

                public double OverdueRate { get; set; }

                public double ReopenRate { get; set; }
    }

                    public class EmployeeKpi
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }

        public double SlaCompliancePercent { get; set; }
        public double AvgFirstResponseHours { get; set; }
        public double AvgResolutionHours { get; set; }
        public double OverdueRate { get; set; }
        public double ReopenRate { get; set; }

        public int FeedbackCount { get; set; }
        public double AvgRating { get; set; }
    }
}