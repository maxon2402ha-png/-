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
                                                                    public class RebalancingService
    {
        private readonly AppDbContext _context;
        private readonly WorkloadService _workloadService;

        public RebalancingService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _workloadService = new WorkloadService(context);
        }

                                                public async Task<RebalancingResult> GetRecommendationsAsync(
            CancellationToken ct = default)
        {
            var result = new RebalancingResult();

                        var workloads = await _workloadService.GetWorkloadAsync(ct);

            var overloaded = workloads
                .Where(w => w.Level == WorkloadLevel.Overloaded)
                .OrderByDescending(w => w.WeightedLoad)
                .ToList();

                                                var receivers = workloads
                .Where(w => w.Level != WorkloadLevel.Overloaded)
                .ToDictionary(w => w.EmployeeId, w => new ReceiverState(w));

            result.OverloadedCount = overloaded.Count;

            if (overloaded.Count == 0 || receivers.Count == 0)
                return result; 
                        var overloadedIds = overloaded.Select(w => w.EmployeeId).ToList();

            var movableTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AssigneeEmployeeId != null
                         && overloadedIds.Contains(t.AssigneeEmployeeId.Value)
                         && t.Status != Constants.TicketStatus.Closed
                         && t.Status != Constants.TicketStatus.Resolved
                                                  && t.Priority != TicketPriority.Critical)
                .Select(t => new { t.Id, t.Title, t.Priority, t.AssigneeEmployeeId })
                .ToListAsync(ct);

                                                foreach (var donor in overloaded)
            {
                                int donorLoad = donor.WeightedLoad;
                int donorTarget = WeightedLimitOf(donor); 
                                var donorTickets = movableTickets
                    .Where(t => t.AssigneeEmployeeId == donor.EmployeeId)
                    .OrderBy(t => (int)t.Priority)
                    .ToList();

                foreach (var ticket in donorTickets)
                {
                                        if (donorLoad <= donorTarget)
                        break;

                    int weight = WorkloadService.GetTicketWeight(ticket.Priority);

                                                            var bestReceiver = receivers.Values
                        .Where(r => r.WeightedLoad + weight <= r.WeightedLimit)
                        .OrderBy(r => r.WeightedLoad)
                        .FirstOrDefault();

                    if (bestReceiver == null)
                        continue; 
                                        result.Recommendations.Add(new RebalancingRecommendation
                    {
                        TicketId = ticket.Id,
                        TicketTitle = ticket.Title,
                        Priority = ticket.Priority.ToString(),
                        FromEmployeeId = donor.EmployeeId,
                        FromEmployeeName = donor.Name,
                        ToEmployeeId = bestReceiver.EmployeeId,
                        ToEmployeeName = bestReceiver.Name
                    });

                                        donorLoad -= weight;
                    bestReceiver.WeightedLoad += weight;
                }
            }

            return result;
        }

                                                public async Task<bool> ApplyReassignmentAsync(
            int ticketId,
            int toEmployeeId,
            string performedBy,
            CancellationToken ct = default)
        {
            var ticket = await _context.Tickets
                .Include(t => t.History)
                .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

            if (ticket == null)
                return false;

            var newAssignee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == toEmployeeId, ct);

            if (newAssignee == null)
                return false;

            int? oldAssigneeId = ticket.AssigneeEmployeeId;
            ticket.AssigneeEmployeeId = toEmployeeId;
            ticket.UpdatedAt = DateTime.UtcNow;

            ticket.History.Add(new TicketHistory
            {
                Action = "Перераспределение",
                Details = $"Тикет передан сотруднику {newAssignee.Name} " +
                          $"(балансировка нагрузки). Инициатор: {performedBy}.",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            return true;
        }

                                                private static int WeightedLimitOf(EmployeeWorkload w)
        {
            int limit = w.MaxActiveTickets > 0 ? w.MaxActiveTickets : 5;
            return limit * WorkloadService.GetTicketWeight(TicketPriority.Normal);
        }

                                        private class ReceiverState
        {
            public int EmployeeId { get; }
            public string Name { get; }
            public int WeightedLoad { get; set; }
            public int WeightedLimit { get; }

            public ReceiverState(EmployeeWorkload w)
            {
                EmployeeId = w.EmployeeId;
                Name = w.Name;
                WeightedLoad = w.WeightedLoad;
                WeightedLimit = WeightedLimitOf(w);
            }
        }
    }

                public class RebalancingResult
    {
                public int OverloadedCount { get; set; }

                public List<RebalancingRecommendation> Recommendations { get; set; } = new();

                public bool HasRecommendations => Recommendations.Count > 0;
    }

                public class RebalancingRecommendation
    {
        public int TicketId { get; set; }
        public string TicketTitle { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;

        public int FromEmployeeId { get; set; }
        public string FromEmployeeName { get; set; } = string.Empty;

        public int ToEmployeeId { get; set; }
        public string ToEmployeeName { get; set; } = string.Empty;

                public string Description =>
            $"#{TicketId} «{TicketTitle}»: передать от {FromEmployeeName} к {ToEmployeeName}";
    }
}
