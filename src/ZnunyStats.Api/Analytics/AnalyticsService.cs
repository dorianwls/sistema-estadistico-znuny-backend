using Microsoft.Extensions.Options;
using ZnunyStats.Api.Data;

namespace ZnunyStats.Api.Analytics;

/// <summary>Arma las respuestas de la API a partir de la instantánea y las fórmulas de <see cref="Kpis"/>.</summary>
public sealed class AnalyticsService(TicketStore store, ZnunyQueries queries, IOptions<StatsOptions> options, TimeProvider clock)
{
    private const int DefaultPeriodDays = 30;
    private readonly StatsOptions _o = options.Value;
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;

    // ---------- Análisis ----------

    public async Task<OverviewResponse> OverviewAsync(TicketQuery q, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var period = ResolvePeriod(q);
        var scoped = ApplyFilters(snap.Tickets, q).ToList();
        var current = InPeriod(scoped, period.From, period.To);
        var previous = InPeriod(scoped, period.PreviousFrom, period.PreviousTo);
        var openNow = scoped.Where(t => t.IsOpen).ToList();

        var bucketDays = period.Days <= 45 ? 1 : 7;
        return new OverviewResponse(
            Meta(snap, period, current.Count),
            new CurrentState(openNow.Count, openNow.Count(t => t.IsOverdue(NowUtc)), openNow.Count(t => t.AgentId is null)),
            new Volume(current.Count, previous.Count, current.Count(t => t.IsResolved), previous.Count(t => t.IsResolved)),
            BuildIndicators(current),
            BuildIndicators(previous),
            bucketDays == 1 ? "day" : "week",
            Trend(current, previous, period, bucketDays),
            CountBy(current, t => t.Status, TicketStatus.All),
            CountBy(current, t => t.Area).Take(8).ToList(),
            CountBy(current, t => t.Unit).Take(8).ToList(),
            current
                .GroupBy(t => t.Category)
                .Select(g => (Label: g.Key, Kpi: Kpis.ResolutionTime(g.ToList())))
                .Where(x => x.Kpi.Sample > 0)
                .OrderByDescending(x => x.Kpi.Sample)
                .Take(8)
                .Select(x => new DurationItem(x.Label, x.Kpi.MedianSeconds, x.Kpi.Sample))
                .ToList(),
            Notices(snap, current));
    }

    // ---------- Agentes ----------

    public async Task<AgentsResponse> AgentsAsync(TicketQuery q, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var period = ResolvePeriod(q);
        var current = InPeriod(ApplyFilters(snap.Tickets, q), period.From, period.To);

        // Orden por volumen a cargo, no por "puntaje": la comparación entre agentes
        // depende de la complejidad y el tipo de casos de cada uno.
        var agents = current
            .GroupBy(t => (t.AgentId, t.AgentName))
            .Select(g =>
            {
                var tickets = g.ToList();
                return new AgentScore(
                    g.Key.AgentId,
                    g.Key.AgentName,
                    tickets.Count,
                    tickets.Count(t => t.IsResolved),
                    tickets.Count(t => t.IsOpen),
                    tickets.Count(t => t.IsOverdue(NowUtc)),
                    tickets.Count(t => t.BulkClosed),
                    tickets.GroupBy(t => t.Unit).OrderByDescending(u => u.Count()).Select(u => u.Key).ToList(),
                    BuildIndicators(tickets));
            })
            .OrderBy(a => a.AgentId is null)
            .ThenByDescending(a => a.Assigned)
            .ToList();

        return new AgentsResponse(Meta(snap, period, current.Count), BuildIndicators(current), agents, Notices(snap, current));
    }

    // ---------- Tickets ----------

    /// <param name="view">"all" (más recientes primero) o "attention" (abiertos, atrasados primero).</param>
    public async Task<TicketPage> TicketsAsync(TicketQuery q, string? view, int page, int pageSize, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var period = ResolvePeriod(q);
        IEnumerable<TicketRecord> tickets = InPeriod(ApplyFilters(snap.Tickets, q), period.From, period.To);

        tickets = view == "attention"
            ? tickets.Where(t => t.IsOpen).OrderByDescending(t => t.IsOverdue(NowUtc)).ThenBy(t => t.CreatedAt)
            : tickets.OrderByDescending(t => t.CreatedAt);

        var list = tickets.ToList();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(ToRow).ToList();
        return new TicketPage(list.Count, page, pageSize, items);
    }

    public async Task<IReadOnlyList<TicketRow>> AllTicketsAsync(TicketQuery q, string? view, CancellationToken ct) =>
        (await TicketsAsync(q, view, 1, int.MaxValue, ct)).Items;

    public async Task<TicketTimeline?> TimelineAsync(long ticketId, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var ticket = snap.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket is null) return null;

        var history = await queries.GetTicketHistoryAsync(ticketId, _o.SourceTimeZone, ct);
        var events = history
            .Select(h => new TimelineEvent(
                ToLocal(h.At),
                h.Type,
                HistoryText.Describe(h.Type, h.Detail),
                snap.AgentNames.TryGetValue(h.ActorId, out var name) ? name : "Sistema",
                h.State,
                h.Queue))
            .ToList();
        return new TicketTimeline(ToRow(ticket), events);
    }

    // ---------- Mapa institucional ----------

    public async Task<LocationsResponse> LocationsAsync(TicketQuery q, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var period = ResolvePeriod(q);
        var current = InPeriod(ApplyFilters(snap.Tickets, q with { Location = null }), period.From, period.To);

        var locations = _o.Locations
            .Select(l =>
            {
                var tickets = current.Where(t => t.LocationKey == l.Key).ToList();
                return new LocationSummary(
                    l.Key,
                    l.Name,
                    l.Latitude,
                    l.Longitude,
                    tickets.Count,
                    tickets.Count(t => t.IsOpen),
                    tickets.Count(t => t.IsOverdue(NowUtc)),
                    tickets.Count(t => t.IsResolved),
                    BuildIndicators(tickets),
                    CountBy(tickets, t => t.Unit));
            })
            .ToList();
        return new LocationsResponse(Meta(snap, period, current.Count), locations);
    }

    // ---------- Catálogos y calidad ----------

    public async Task<Catalogs> CatalogsAsync(CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var t = snap.Tickets.Where(x => x.IsCountable).ToList();
        var period = ResolvePeriod(new TicketQuery(null, null, null, null, null, null, null, null, null));

        static IReadOnlyList<Option> Distinct(IEnumerable<string> values) =>
            values.Distinct().Order(StringComparer.CurrentCulture).Select(v => new Option(v, v)).ToList();

        return new Catalogs(
            Distinct(t.Select(x => x.Area)),
            Distinct(t.Select(x => x.Unit)),
            Distinct(t.Select(x => x.Category)),
            t.GroupBy(x => x.PriorityId).OrderByDescending(g => g.Key)
                .Select(g => new Option(g.Key.ToString(), g.First().Priority)).ToList(),
            TicketStatus.All.Select(s => new Option(s, s)).ToList(),
            t.Where(x => x.AgentId is not null).GroupBy(x => x.AgentId!.Value)
                .Select(g => new Option(g.Key.ToString(), g.First().AgentName)).OrderBy(o => o.Label).ToList(),
            _o.Locations.Select(l => new Option(l.Key, l.Name)).ToList(),
            t.Count > 0 ? LocalDate(t.Min(x => x.CreatedAt)) : null,
            t.Count > 0 ? LocalDate(t.Max(x => x.CreatedAt)) : null,
            period.From,
            period.To,
            _o.ResolutionTargetHours
                .OrderByDescending(kv => kv.Key)
                .Select(kv => new TargetInfo(TicketStore.PriorityLabel(kv.Key), kv.Value)).ToList(),
            _o.ReopenWindowDays);
    }

    public async Task<DataQualityResponse> DataQualityAsync(TicketQuery q, CancellationToken ct)
    {
        var snap = await store.GetAsync(ct);
        var period = ResolvePeriod(q);
        var current = InPeriod(ApplyFilters(snap.Tickets, q), period.From, period.To);
        var resolved = current.Where(t => t.ResolvedAt is not null).ToList();
        var open = current.Where(t => t.IsOpen).ToList();

        var checks = new List<QualityCheck>
        {
            new("BULK_CLOSED", Severity(resolved.Count(t => t.BulkClosed), resolved.Count),
                "Cierres masivos",
                "Tickets cerrados con la acción masiva de Znuny. Cuentan para eficacia, pero se excluyen de tiempos y efectividad porque su duración no refleja el trabajo real.",
                resolved.Count(t => t.BulkClosed), resolved.Count, ["eficiencia", "efectividad"]),
            new("OPEN_UNASSIGNED", Severity(open.Count(t => t.AgentId is null), open.Count),
                "Abiertos sin agente",
                "Tickets abiertos cuyo propietario es una cuenta de sistema. No se pueden atribuir a ningún agente.",
                open.Count(t => t.AgentId is null), open.Count, ["agentes"]),
            new("NO_AREA", Severity(current.Count(t => t.Area == "Sin área registrada"), current.Count),
                "Solicitante sin área",
                "El cliente del ticket no tiene una empresa/área asociada en Znuny.",
                current.Count(t => t.Area == "Sin área registrada"), current.Count, ["área solicitante"]),
            new("RESOLVED_WITHOUT_ATTENTION", Severity(resolved.Count(t => t.FirstAttentionAt is null), resolved.Count),
                "Resueltos sin acción de agente",
                "Tickets resueltos sin ninguna acción registrada por una persona (cerrados por el sistema).",
                resolved.Count(t => t.FirstAttentionAt is null), resolved.Count, ["primera atención"]),
            snap.Capabilities.ActiveSlas == 0
                ? new("NO_SLA", "warning", "Sin SLA configurado",
                    "Znuny no tiene SLA activos. La efectividad usa objetivos de referencia por prioridad definidos en la configuración del backend.",
                    null, null, ["efectividad"])
                : new("SLA_AVAILABLE", "ok", "SLA configurado", "Existen SLA activos en Znuny.", null, null, []),
            snap.Capabilities.TimeAccountingEntries == 0
                ? new("NO_TIME_ACCOUNTING", "info", "Sin registro de tiempo",
                    "Los agentes no registran tiempo trabajado (time_accounting vacío). No se puede medir esfuerzo, solo tiempo transcurrido.",
                    null, null, ["eficiencia"])
                : new("TIME_ACCOUNTING", "ok", "Registro de tiempo disponible", "Existen registros de tiempo trabajado.", null, null, []),
        };
        return new DataQualityResponse(Meta(snap, period, current.Count), checks);
    }

    // ---------- Piezas comunes ----------

    private Indicators BuildIndicators(IReadOnlyCollection<TicketRecord> tickets) => new(
        Kpis.Eficacia(tickets),
        Kpis.Efectividad(tickets, NowUtc, _o.ReopenWindowDays),
        Kpis.ResolutionTime(tickets),
        Kpis.FirstAttentionTime(tickets),
        Kpis.ReopenRate(tickets, NowUtc, _o.ReopenWindowDays));

    private IEnumerable<TicketRecord> ApplyFilters(IEnumerable<TicketRecord> tickets, TicketQuery q) =>
        tickets.Where(t => t.IsCountable
            && (q.Area is null || t.Area == q.Area)
            && (q.Unit is null || t.Unit == q.Unit)
            && (q.Category is null || t.Category == q.Category)
            && (q.Status is null || t.Status == q.Status)
            && (q.AgentId is null || t.AgentId == q.AgentId)
            && (q.PriorityId is null || t.PriorityId == q.PriorityId)
            && (q.Location is null || t.LocationKey == q.Location));

    private List<TicketRecord> InPeriod(IEnumerable<TicketRecord> tickets, DateOnly from, DateOnly to)
    {
        var start = StartOfDayUtc(from);
        var end = StartOfDayUtc(to.AddDays(1));
        return tickets.Where(t => t.CreatedAt >= start && t.CreatedAt < end).ToList();
    }

    private List<TrendPoint> Trend(List<TicketRecord> current, List<TicketRecord> previous, Period p, int bucketDays)
    {
        var points = new List<TrendPoint>();
        for (var offset = 0; offset < p.Days; offset += bucketDays)
        {
            var len = Math.Min(bucketDays, p.Days - offset);
            int Count(List<TicketRecord> list, DateOnly start) =>
                list.Count(t => LocalDate(t.CreatedAt) is var d && d >= start && d < start.AddDays(len));

            var start = p.From.AddDays(offset);
            points.Add(new TrendPoint(start.ToString("dd MMM", Culture), Count(current, start), Count(previous, p.PreviousFrom.AddDays(offset))));
        }
        return points;
    }

    private static List<CountItem> CountBy(IReadOnlyCollection<TicketRecord> tickets, Func<TicketRecord, string> key, IEnumerable<string>? order = null)
    {
        var counts = tickets.GroupBy(key).ToDictionary(g => g.Key, g => g.Count());
        var labels = order?.ToList() ?? counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        double Percent(int count) => tickets.Count == 0 ? 0 : Math.Round(100.0 * count / tickets.Count, 1);
        return labels.Select(l => new CountItem(l, counts.GetValueOrDefault(l), Percent(counts.GetValueOrDefault(l)))).ToList();
    }

    private List<Notice> Notices(Snapshot snap, IReadOnlyCollection<TicketRecord> current)
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

    private TicketRow ToRow(TicketRecord t) => new(
        t.Id, t.Number, t.Title, ToLocal(t.CreatedAt), t.Area, t.Unit, t.Category,
        _o.Locations.FirstOrDefault(l => l.Key == t.LocationKey)?.Name ?? t.LocationKey,
        t.Priority, t.Status, t.AgentName,
        t.FirstAttentionSeconds, t.ResolutionSeconds, t.Target.TotalSeconds,
        Math.Round(((t.ResolvedAt ?? NowUtc) - t.CreatedAt).TotalSeconds),
        t.IsOverdue(NowUtc), t.BulkClosed, t.ReopenedWithin(_o.ReopenWindowDays));

    private Meta Meta(Snapshot snap, Period p, int population) =>
        new(p.From, p.To, p.PreviousFrom, p.PreviousTo, _o.TimeZone, ToLocal(snap.LoadedAtUtc), population);

    private static string Severity(int affected, int outOf) =>
        affected == 0 ? "ok" : outOf > 0 && affected * 100 / outOf >= 25 ? "warning" : "info";

    // ---------- Fechas ----------

    private static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("es-NI");

    public sealed record Period(DateOnly From, DateOnly To)
    {
        public int Days => To.DayNumber - From.DayNumber + 1;
        public DateOnly PreviousTo => From.AddDays(-1);
        public DateOnly PreviousFrom => From.AddDays(-Days);
    }

    /// <summary>Por defecto: los últimos 30 días hasta hoy (hora local).</summary>
    public Period ResolvePeriod(TicketQuery q)
    {
        var today = LocalDate(NowUtc);
        var to = q.To ?? today;
        var from = q.From ?? to.AddDays(-(DefaultPeriodDays - 1));
        if (from > to) throw new ArgumentException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber > 366) throw new ArgumentException("El período máximo es de un año.");
        return new Period(from, to);
    }

    private DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _tz));

    private DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _tz);

    private DateTimeOffset ToLocal(DateTime utc)
    {
        var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(u, _tz), _tz.GetUtcOffset(u));
    }
}
