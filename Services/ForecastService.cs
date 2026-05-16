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
                                                            public class ForecastService
    {
        private readonly AppDbContext _context;

                private const int HistoryDays = 56; 
        public ForecastService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

                                        public async Task<ForecastResult> ForecastTicketInflowAsync(
            int daysAhead = 7,
            CancellationToken ct = default)
        {
            if (daysAhead < 1) daysAhead = 1;
            if (daysAhead > 30) daysAhead = 30;

            var today = DateTime.UtcNow.Date;
            var historyStart = today.AddDays(-HistoryDays);

                        var createdDates = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.CreatedAt >= historyStart)
                .Select(t => t.CreatedAt)
                .ToListAsync(ct);

                        var countByDay = createdDates
                .GroupBy(d => d.Date)
                .ToDictionary(g => g.Key, g => g.Count());

                        var series = new List<DailyCount>();
            for (var day = historyStart; day < today; day = day.AddDays(1))
            {
                series.Add(new DailyCount
                {
                    Date = day,
                    Count = countByDay.TryGetValue(day, out var c) ? c : 0
                });
            }

            var result = new ForecastResult
            {
                HistoryFrom = historyStart,
                HistoryTo = today.AddDays(-1)
            };

                        if (series.Count == 0 || series.All(d => d.Count == 0))
            {
                result.HasEnoughData = false;
                return result;
            }

                        double baseline = series.Average(d => d.Count);
            result.AveragePerDay = Math.Round(baseline, 2);

                        var weekdayFactors = CalculateWeekdayFactors(series, baseline);

                        for (int i = 1; i <= daysAhead; i++)
            {
                var date = today.AddDays(i);
                double factor = weekdayFactors[date.DayOfWeek];
                double predicted = baseline * factor;

                result.Points.Add(new ForecastPoint
                {
                    Date = date,
                                                                                PredictedTickets = Math.Round(predicted, MidpointRounding.AwayFromZero),
                    DayOfWeek = GetWeekdayName(date.DayOfWeek)
                });
            }

            result.HasEnoughData = true;
            result.TotalPredicted = Math.Round(result.Points.Sum(p => p.PredictedTickets), 1);

            return result;
        }

                                                                public async Task<SlaRiskAssessment> AssessSlaRiskAsync(
            int daysAhead = 7,
            CancellationToken ct = default)
        {
            var forecast = await ForecastTicketInflowAsync(daysAhead, ct);

            var assessment = new SlaRiskAssessment
            {
                ForecastedInflow = forecast.TotalPredicted,
                HasEnoughData = forecast.HasEnoughData
            };

                                    if (!forecast.HasEnoughData)
            {
                assessment.RiskLevel = SlaRiskLevel.Unknown;
                return assessment;
            }

            var today = DateTime.UtcNow.Date;
            var historyStart = today.AddDays(-HistoryDays);

                        int closedCount = await _context.Tickets
                .AsNoTracking()
                .CountAsync(t => t.ClosedAt != null
                              && t.ClosedAt >= historyStart
                              && t.ClosedAt < today, ct);

                        double capacityPerDay = (double)closedCount / HistoryDays;
            assessment.TeamCapacity = Math.Round(capacityPerDay * daysAhead, 1);

                        int backlog = await _context.Tickets
                .AsNoTracking()
                .CountAsync(t => t.Status != Constants.TicketStatus.Closed
                              && t.Status != Constants.TicketStatus.Resolved, ct);
            assessment.CurrentBacklog = backlog;

                        double expectedLoad = backlog + forecast.TotalPredicted;
            assessment.ExpectedTotalLoad = Math.Round(expectedLoad, 1);

                                    if (assessment.TeamCapacity > 0)
            {
                assessment.LoadRatio = Math.Round(expectedLoad / assessment.TeamCapacity, 2);
            }

            assessment.RiskLevel = ClassifyRisk(assessment.LoadRatio);

            return assessment;
        }

                        
                                                private static Dictionary<DayOfWeek, double> CalculateWeekdayFactors(
            List<DailyCount> series,
            double baseline)
        {
            var factors = new Dictionary<DayOfWeek, double>();

            foreach (DayOfWeek dow in Enum.GetValues(typeof(DayOfWeek)))
            {
                var daysOfThisType = series
                    .Where(d => d.Date.DayOfWeek == dow)
                    .ToList();

                if (daysOfThisType.Count == 0 || baseline <= 0)
                {
                                        factors[dow] = 1.0;
                    continue;
                }

                double avgForDay = daysOfThisType.Average(d => d.Count);
                factors[dow] = avgForDay / baseline;
            }

            return factors;
        }

                                private static SlaRiskLevel ClassifyRisk(double loadRatio)
        {
            if (loadRatio <= 0) return SlaRiskLevel.Low;
            if (loadRatio < 0.8) return SlaRiskLevel.Low;
            if (loadRatio <= 1.1) return SlaRiskLevel.Medium;
            return SlaRiskLevel.High;
        }

                                private static string GetWeekdayName(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Monday => "Понедельник",
            DayOfWeek.Tuesday => "Вторник",
            DayOfWeek.Wednesday => "Среда",
            DayOfWeek.Thursday => "Четверг",
            DayOfWeek.Friday => "Пятница",
            DayOfWeek.Saturday => "Суббота",
            DayOfWeek.Sunday => "Воскресенье",
            _ => dow.ToString()
        };

                private class DailyCount
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }
        }
    }

                public enum SlaRiskLevel
    {
                Unknown = -1,

                Low = 0,

                Medium = 1,

                High = 2
    }

                public class ForecastResult
    {
                public bool HasEnoughData { get; set; }

                public DateTime HistoryFrom { get; set; }

                public DateTime HistoryTo { get; set; }

                public double AveragePerDay { get; set; }

                public double TotalPredicted { get; set; }

                public List<ForecastPoint> Points { get; set; } = new();
    }

                public class ForecastPoint
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;

                public double PredictedTickets { get; set; }

                public string DateLabel => Date.ToString("dd.MM");
    }

                public class SlaRiskAssessment
    {
        public bool HasEnoughData { get; set; }

                public double ForecastedInflow { get; set; }

                public int CurrentBacklog { get; set; }

                public double ExpectedTotalLoad { get; set; }

                public double TeamCapacity { get; set; }

                public double LoadRatio { get; set; }

                public SlaRiskLevel RiskLevel { get; set; }

                public string RiskText => RiskLevel switch
        {
            SlaRiskLevel.High => "Высокий риск срыва SLA",
            SlaRiskLevel.Medium => "Средний риск",
            SlaRiskLevel.Low => "Низкий риск",
            _ => "Нет данных"
        };
    }
}