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
                public enum WorkloadLevel
    {
                Normal = 0,

                High = 1,

                Overloaded = 2
    }

                                                public class WorkloadService
    {
        private readonly AppDbContext _context;

                                                private static readonly Dictionary<TicketPriority, int> PriorityWeights = new()
        {
            { TicketPriority.Low,      1 },
            { TicketPriority.Normal,   2 },
            { TicketPriority.High,     3 },
            { TicketPriority.Critical, 4 }
        };

                private const double HighLoadThreshold = 0.70;

        public WorkloadService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

                                public static int GetTicketWeight(TicketPriority priority)
            => PriorityWeights.TryGetValue(priority, out var w) ? w : 2;

                                                public async Task<List<EmployeeWorkload>> GetWorkloadAsync(CancellationToken ct = default)
        {
                        var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.User)
                .Where(e => e.User.Role == Constants.UserRoles.Support
                         || e.User.Role == Constants.UserRoles.Admin)
                .ToListAsync(ct);

                        var activeTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AssigneeEmployeeId != null
                         && t.Status != Constants.TicketStatus.Closed
                         && t.Status != Constants.TicketStatus.Resolved)
                .Select(t => new { t.AssigneeEmployeeId, t.Priority, t.Status })
                .ToListAsync(ct);

            var result = new List<EmployeeWorkload>();

            foreach (var emp in employees)
            {
                var empTickets = activeTickets
                    .Where(t => t.AssigneeEmployeeId == emp.Id)
                    .ToList();

                int activeCount = empTickets.Count;

                                int weightedLoad = empTickets.Sum(t => GetTicketWeight(t.Priority));

                                                int limit = emp.MaxActiveTickets > 0 ? emp.MaxActiveTickets : 5;

                                                                int weightedLimit = limit * GetTicketWeight(TicketPriority.Normal);

                double loadRatio = weightedLimit > 0
                    ? (double)weightedLoad / weightedLimit
                    : 0.0;

                result.Add(new EmployeeWorkload
                {
                    EmployeeId = emp.Id,
                    Name = emp.Name,
                    Username = emp.User?.Username ?? "—",
                    ActiveTickets = activeCount,
                    WeightedLoad = weightedLoad,
                    MaxActiveTickets = limit,
                    LoadPercent = Math.Round(loadRatio * 100.0, 1),
                    Level = ClassifyLevel(loadRatio),
                    CriticalCount = empTickets.Count(t => t.Priority == TicketPriority.Critical),
                    HighCount = empTickets.Count(t => t.Priority == TicketPriority.High)
                });
            }

            return result
                .OrderByDescending(w => w.LoadPercent)
                .ToList();
        }

                                                                        public async Task<int?> GetLeastLoadedEmployeeAsync(CancellationToken ct = default)
        {
            var workloads = await GetWorkloadAsync(ct);

            if (workloads.Count == 0)
                return null;

                        var available = workloads
                .Where(w => w.Level != WorkloadLevel.Overloaded)
                .ToList();

                        var pool = available.Count > 0 ? available : workloads;

            return pool
                .OrderBy(w => w.WeightedLoad)
                .ThenBy(w => w.ActiveTickets)
                .First()
                .EmployeeId;
        }

                                        public async Task<WorkloadSummary> GetSummaryAsync(CancellationToken ct = default)
        {
            var workloads = await GetWorkloadAsync(ct);

            return new WorkloadSummary
            {
                TotalEmployees = workloads.Count,
                OverloadedCount = workloads.Count(w => w.Level == WorkloadLevel.Overloaded),
                HighLoadCount = workloads.Count(w => w.Level == WorkloadLevel.High),
                NormalCount = workloads.Count(w => w.Level == WorkloadLevel.Normal),
                AvgLoadPercent = workloads.Count > 0
                    ? Math.Round(workloads.Average(w => w.LoadPercent), 1)
                    : 0.0,
                TotalActiveTickets = workloads.Sum(w => w.ActiveTickets)
            };
        }

                                private static WorkloadLevel ClassifyLevel(double loadRatio)
        {
            if (loadRatio > 1.0)
                return WorkloadLevel.Overloaded;

            if (loadRatio >= HighLoadThreshold)
                return WorkloadLevel.High;

            return WorkloadLevel.Normal;
        }
    }

                public class EmployeeWorkload
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

                public int ActiveTickets { get; set; }

                public int WeightedLoad { get; set; }

                public int MaxActiveTickets { get; set; }

                public double LoadPercent { get; set; }

                public WorkloadLevel Level { get; set; }

                public int CriticalCount { get; set; }

                public int HighCount { get; set; }

                public string LevelText => Level switch
        {
            WorkloadLevel.Overloaded => "Перегружен",
            WorkloadLevel.High => "Высокая загрузка",
            _ => "Норма"
        };
    }

                public class WorkloadSummary
    {
        public int TotalEmployees { get; set; }
        public int OverloadedCount { get; set; }
        public int HighLoadCount { get; set; }
        public int NormalCount { get; set; }
        public double AvgLoadPercent { get; set; }
        public int TotalActiveTickets { get; set; }
    }
}