using System.Globalization;

using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Domain.Entities;
using ZnunyStats.Domain.Metrics;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Analytics.GetOverview;

/// <summary>Arma el módulo Análisis. Lo usan el endpoint y el reporte de servicio.</summary>
public static class GetOverviewHandler
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-NI");

    public static async Task<OverviewResponse> ExecuteAsync(TicketQuery q, TicketAnalytics a, CancellationToken ct)
    {
        var snap = await a.GetSnapshotAsync(ct);
        var period = a.ResolvePeriod(q);
        var scoped = a.ApplyFilters(snap.Tickets, q).ToList();
        var current = a.InPeriod(scoped, period.From, period.To);
        var previous = a.InPeriod(scoped, period.PreviousFrom, period.PreviousTo);
        var openNow = scoped.Where(t => t.IsOpen).ToList();
        var now = a.NowUtc;

        var bucketDays = period.Days <= 45 ? 1 : 7;
        return new OverviewResponse(
            a.Meta(snap, period, current.Count),
            new CurrentState(openNow.Count, openNow.Count(t => t.IsOverdue(now)), openNow.Count(t => t.AgentId is null)),
            new Volume(current.Count, previous.Count, current.Count(t => t.IsResolved), previous.Count(t => t.IsResolved)),
            a.BuildIndicators(current),
            a.BuildIndicators(previous),
            bucketDays == 1 ? "day" : "week",
            Trend(a, current, previous, period, bucketDays),
            TicketAnalytics.CountBy(current, t => t.Status, TicketStatus.All),
            TicketAnalytics.CountBy(current, t => t.Area).Take(8).ToList(),
            TicketAnalytics.CountBy(current, t => t.Unit).Take(8).ToList(),
            current
                .GroupBy(t => t.Category)
                .Select(g => (Label: g.Key, Kpi: Kpis.ResolutionTime(g.ToList())))
                .Where(x => x.Kpi.Sample > 0)
                .OrderByDescending(x => x.Kpi.Sample)
                .Take(8)
                .Select(x => new DurationItem(x.Label, x.Kpi.MedianSeconds, x.Kpi.Sample))
                .ToList(),
            Series(a, current, period, bucketDays),
            Outcomes(a, current),
            ResolutionDistribution(current),
            ByPriority(a, current),
            GroupScores(a, current, t => t.Unit),
            GroupScores(a, current, t => t.Area),
            a.Notices(snap, current));
    }

    private static List<TrendPoint> Trend(TicketAnalytics a, List<TicketRecord> current, List<TicketRecord> previous, Period p, int bucketDays)
    {
        var points = new List<TrendPoint>();
        for (var offset = 0; offset < p.Days; offset += bucketDays)
        {
            var len = Math.Min(bucketDays, p.Days - offset);
            int Count(List<TicketRecord> list, DateOnly start) =>
                list.Count(t => a.Clock.LocalDate(t.CreatedAt) is var d && d >= start && d < start.AddDays(len));

            var start = p.From.AddDays(offset);
            points.Add(new TrendPoint(start.ToString("dd MMM", Culture), Count(current, start), Count(previous, p.PreviousFrom.AddDays(offset))));
        }
        return points;
    }

    private static List<SeriesPoint> Series(TicketAnalytics a, List<TicketRecord> current, Period p, int bucketDays)
    {
        var points = new List<SeriesPoint>();
        for (var offset = 0; offset < p.Days; offset += bucketDays)
        {
            var start = p.From.AddDays(offset);
            var end = start.AddDays(Math.Min(bucketDays, p.Days - offset));
            var bucket = current.Where(t => a.Clock.LocalDate(t.CreatedAt) is var d && d >= start && d < end).ToList();
            var i = a.BuildIndicators(bucket);
            points.Add(new SeriesPoint(
                start.ToString("dd MMM", Culture),
                bucket.Count,
                bucket.Count(t => t.IsResolved),
                bucket.Count(t => t.Status == TicketStatus.ClosedUnsuccessful),
                bucket.Count(t => t.IsOpen),
                i.Eficacia.Percent,
                i.Efectividad.Percent,
                i.ResolutionTime.MedianSeconds));
        }
        return points;
    }

    private static readonly (Outcome Outcome, string Key, string Label)[] OutcomeLabels =
    [
        (Outcome.OnTime, "onTime", "A tiempo y sin reabrir"),
        (Outcome.Late, "late", "Resuelto fuera del objetivo"),
        (Outcome.Reopened, "reopened", "Se reabrió"),
        (Outcome.Overdue, "overdue", "Abierto y vencido"),
        (Outcome.Unsuccessful, "unsuccessful", "Cerrado sin éxito"),
        (Outcome.Pending, "pending", "Abierto, aún en plazo"),
        (Outcome.Excluded, "excluded", "Cierre masivo (no se evalúa)"),
    ];

    private static List<OutcomeItem> Outcomes(TicketAnalytics a, List<TicketRecord> current)
    {
        var counts = current
            .GroupBy(t => Kpis.Classify(t, a.NowUtc, a.Options.ReopenWindowDays))
            .ToDictionary(g => g.Key, g => g.Count());
        return OutcomeLabels.Select(o => new OutcomeItem(o.Key, o.Label, counts.GetValueOrDefault(o.Outcome))).ToList();
    }

    private static readonly (string Label, double UpToHours)[] ResolutionBands =
    [
        ("Menos de 30 min", 0.5), ("30 min a 2 h", 2), ("2 a 8 h", 8), ("8 a 24 h", 24), ("1 a 3 días", 72), ("Más de 3 días", double.MaxValue),
    ];

    /// <summary>Cuántos tickets se resolvieron en cada rango de tiempo. Sin cierres masivos, igual que la mediana.</summary>
    private static List<CountItem> ResolutionDistribution(List<TicketRecord> current)
    {
        var hours = current.Where(t => t.ResolvedAt is not null && !t.BulkClosed).Select(t => t.ResolutionSeconds!.Value / 3600).ToList();
        var lower = 0d;
        return ResolutionBands.Select(b =>
        {
            var from = lower;
            lower = b.UpToHours;
            var count = hours.Count(h => h >= from && h < b.UpToHours);
            return new CountItem(b.Label, count, hours.Count == 0 ? 0 : Math.Round(100.0 * count / hours.Count, 1));
        }).ToList();
    }

    private static List<PriorityCompliance> ByPriority(TicketAnalytics a, List<TicketRecord> current) =>
        current
            .GroupBy(t => (t.PriorityId, t.Priority))
            .OrderByDescending(g => g.Key.PriorityId)
            .Select(g =>
            {
                var tickets = g.ToList();
                var kpi = Kpis.Efectividad(tickets, a.NowUtc, a.Options.ReopenWindowDays);
                return new PriorityCompliance(g.Key.Priority, tickets[0].Target.TotalHours, kpi.Denominator, kpi.Numerator, kpi.Percent,
                    Kpis.ResolutionTime(tickets).MedianSeconds);
            })
            .ToList();

    private static List<GroupScore> GroupScores(TicketAnalytics a, List<TicketRecord> current, Func<TicketRecord, string> key) =>
        current
            .GroupBy(key)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g =>
            {
                var i = a.BuildIndicators(g.ToList());
                return new GroupScore(g.Key, g.Count(), i.Eficacia.Percent, i.Efectividad.Percent, i.ResolutionTime.MedianSeconds);
            })
            .ToList();
}
