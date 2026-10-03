using Microsoft.Extensions.Options;

using ZnunyStats.Api.Common;
using ZnunyStats.Domain.Entities;
using ZnunyStats.Domain.Metrics;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Shared;

/// <summary>
/// Piezas comunes a todos los módulos: instantánea de tickets, filtros, período, indicadores y presentación de filas.
/// Cada módulo arma su respuesta a partir de esto y de las fórmulas de <see cref="Kpis"/>.
/// </summary>
public sealed class TicketAnalytics(TicketStore store, ZnunyClock clock, IOptions<StatsOptions> options)
{
    public StatsOptions Options { get; } = options.Value;

    public ZnunyClock Clock => clock;

    public DateTime NowUtc => clock.UtcNow;

    public Task<Snapshot> GetSnapshotAsync(CancellationToken ct) => store.GetAsync(ct);

    public Period ResolvePeriod(TicketQuery q) => Period.Resolve(q, clock.Today);

    public IEnumerable<TicketRecord> ApplyFilters(IEnumerable<TicketRecord> tickets, TicketQuery q) =>
        tickets.Where(t => t.IsCountable
            && (q.Area is null || t.Area == q.Area)
            && (q.Unit is null || t.Unit == q.Unit)
            && (q.Category is null || t.Category == q.Category)
            && (q.Status is null || t.Status == q.Status)
            && (q.AgentId is null || t.AgentId == q.AgentId)
            && (q.PriorityId is null || t.PriorityId == q.PriorityId)
            && (q.Location is null || t.LocationKey == q.Location));

    /// <summary>Tickets creados entre <paramref name="from"/> y <paramref name="to"/> (fechas locales, inclusivas).</summary>
    public List<TicketRecord> InPeriod(IEnumerable<TicketRecord> tickets, DateOnly from, DateOnly to)
    {
        var start = clock.StartOfDayUtc(from);
        var end = clock.StartOfDayUtc(to.AddDays(1));
        return tickets.Where(t => t.CreatedAt >= start && t.CreatedAt < end).ToList();
    }

    /// <summary>La población habitual: tickets que cumplen los filtros y se crearon en el período.</summary>
    public List<TicketRecord> Population(Snapshot snap, TicketQuery q, Period period) =>
        InPeriod(ApplyFilters(snap.Tickets, q), period.From, period.To);

    public Indicators BuildIndicators(IReadOnlyCollection<TicketRecord> tickets) => new(
        Kpis.Eficacia(tickets),
        Kpis.Efectividad(tickets, NowUtc, Options.ReopenWindowDays),
        Kpis.ResolutionTime(tickets),
        Kpis.FirstAttentionTime(tickets),
        Kpis.ReopenRate(tickets, NowUtc, Options.ReopenWindowDays));

    public Meta Meta(Snapshot snap, Period p, int population) =>
        new(p.From, p.To, p.PreviousFrom, p.PreviousTo, clock.TimeZoneId, clock.ToLocal(snap.LoadedAtUtc), population);

    public List<Notice> Notices(Snapshot snap, IReadOnlyCollection<TicketRecord> current)
    {
        var notices = new List<Notice>();
        var bulk = current.Count(t => t.BulkClosed);
        if (bulk > 0)
            notices.Add(new("BULK_CLOSED", "info",
                $"{bulk} tickets se cerraron con acción masiva: cuentan como resueltos, pero no se usan para medir tiempos ni efectividad."));
        if (snap.Capabilities.ActiveSlas == 0)
            notices.Add(new("NO_SLA", "info",
                "Znuny no tiene SLA configurado; los objetivos de tiempo son referencias internas por prioridad."));
        if (current.Count is > 0 and < 20)
            notices.Add(new("SMALL_SAMPLE", "warning",
                $"Solo hay {current.Count} tickets en la selección: los porcentajes pueden variar mucho con pocos casos."));
        return notices;
    }

    public TicketRow ToRow(TicketRecord t) => new(
        t.Id, t.Number, t.Title, clock.ToLocal(t.CreatedAt), t.Area, t.Unit, t.Category,
        LocationName(t.LocationKey),
        t.Priority, t.Status, t.AgentName,
        t.FirstAttentionSeconds, t.ResolutionSeconds, t.Target.TotalSeconds,
        Math.Round(((t.ResolvedAt ?? NowUtc) - t.CreatedAt).TotalSeconds),
        t.IsOverdue(NowUtc), t.BulkClosed, t.ReopenedWithin(Options.ReopenWindowDays));

    public string LocationName(string key) => Options.Locations.FirstOrDefault(l => l.Key == key)?.Name ?? key;

    public IReadOnlyList<TargetInfo> Targets() =>
        Options.ResolutionTargetHours
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new TargetInfo(TicketStore.PriorityLabel(kv.Key), kv.Value))
            .ToList();

    /// <summary>Los filtros aplicados con nombres legibles (agente y prioridad por nombre, no por id).</summary>
    public async Task<IReadOnlyList<(string Label, string Value)>> DescribeFiltersAsync(TicketQuery q, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var priority = q.PriorityId is { } pid ? snap.Tickets.FirstOrDefault(t => t.PriorityId == pid)?.Priority ?? pid.ToString() : null;
        var agent = q.AgentId is { } aid ? snap.AgentNames.GetValueOrDefault(aid, aid.ToString()) : null;
        var location = q.Location is { } key ? LocationName(key) : null;
        return
        [
            ("Área solicitante", q.Area ?? "Todas"),
            ("Equipo de TI", q.Unit ?? "Todos"),
            ("Categoría", q.Category ?? "Todas"),
            ("Estado", q.Status ?? "Todos"),
            ("Agente", agent ?? "Todos"),
            ("Prioridad", priority ?? "Todas"),
            ("Ubicación", location ?? "Todas"),
        ];
    }

    public static List<CountItem> CountBy(IReadOnlyCollection<TicketRecord> tickets, Func<TicketRecord, string> key, IEnumerable<string>? order = null)
    {
        var counts = tickets.GroupBy(key).ToDictionary(g => g.Key, g => g.Count());
        var labels = order?.ToList() ?? counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        double Percent(int count) => tickets.Count == 0 ? 0 : Math.Round(100.0 * count / tickets.Count, 1);
        return labels.Select(l => new CountItem(l, counts.GetValueOrDefault(l), Percent(counts.GetValueOrDefault(l)))).ToList();
    }
}
