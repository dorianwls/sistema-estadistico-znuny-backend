using Microsoft.Extensions.Options;

using ZnunyStats.Api.Common;
using ZnunyStats.Api.Modules.Agents.GetAgents;
using ZnunyStats.Api.Modules.Analytics.GetOverview;
using ZnunyStats.Api.Modules.DataQuality.GetDataQuality;
using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Api.Modules.Tickets.GetTickets;
using ZnunyStats.Domain.Metrics;
using ZnunyStats.Domain.ReadModels;
using ZnunyStats.Reports.Content;

using static ZnunyStats.Reports.Content.ReportText;

namespace ZnunyStats.Api.Modules.Reports.ExportReport;

/// <summary>Arma el contenido de cada tipo de reporte a partir del servicio de analítica.</summary>
public sealed class ReportContentBuilder(TicketAnalytics analytics, IOptions<StatsOptions> options)
{
    public static readonly string[] Kinds = ["servicio", "atencion", "agentes"];

    private readonly StatsOptions _o = options.Value;

    public async Task<ReportContent?> BuildAsync(string kind, TicketQuery q, CancellationToken ct) => kind switch
    {
        "servicio" => await ServiceAsync(q, ct),
        "atencion" => await AttentionAsync(q, ct),
        // Igual que la vista Agentes: compara al equipo completo, sin filtrar por agente ni estado.
        "agentes" => await AgentsAsync(q with { AgentId = null, Status = null }, ct),
        _ => null,
    };

    private async Task<ReportContent> ServiceAsync(TicketQuery q, CancellationToken ct)
    {
        var o = await GetOverviewHandler.ExecuteAsync(q, analytics, ct);
        var cur = o.Indicators;
        var prev = o.PreviousIndicators;
        return new ReportContent(
            await HeaderAsync("servicio", "Informe del servicio de soporte", "Eficacia, eficiencia y efectividad de la mesa de ayuda en el período.", o.Meta, q, ct),
            KpiLines(cur, prev, o.Volume),
            ServiceHighlights(o),
            o.Notices,
            analytics.Targets(),
            o, null,
            await GetDataQualityHandler.ExecuteAsync(q, analytics, ct),
            await GetTicketsHandler.AllAsync(q, "all", analytics, ct));
    }

    private async Task<ReportContent> AttentionAsync(TicketQuery q, CancellationToken ct)
    {
        var o = await GetOverviewHandler.ExecuteAsync(q, analytics, ct);
        var tickets = await GetTicketsHandler.AllAsync(q, GetTicketsHandler.AttentionView, analytics, ct);
        var overdue = tickets.Count(t => t.Overdue);
        var unassigned = tickets.Count(t => t.Agent == "Sin asignar");
        KpiLine Count(string name, double value, string formula) => new(name, "Pendientes", KpiUnit.Count, value, null, null, KpiStatus.None, formula);

        var highlights = new List<string>
        {
            tickets.Count == 0
                ? "No hay tickets abiertos de los creados en el período."
                : $"{tickets.Count} tickets creados en el período siguen abiertos; {overdue} ya superaron el objetivo de su prioridad.",
        };
        if (unassigned > 0) highlights.Add($"{unassigned} tickets abiertos no tienen agente asignado: conviene repartirlos primero.");
        var oldest = tickets.MaxBy(t => t.AgeSeconds);
        if (oldest is not null) highlights.Add($"El más antiguo es el {oldest.Number} ({oldest.Category}), abierto hace {Duration(oldest.AgeSeconds)}.");

        return new ReportContent(
            await HeaderAsync("atencion", "Tickets que necesitan atención", "Tickets abiertos del período, atrasados primero, para priorizar el trabajo.", o.Meta, q, ct),
            [
                Count("Abiertos", tickets.Count, "Tickets creados en el período que siguen sin resolver."),
                Count("Atrasados", overdue, "Abiertos con más antigüedad que el objetivo de su prioridad."),
                Count("Sin agente", unassigned, "Abiertos cuyo propietario es una cuenta de sistema."),
            ],
            highlights, o.Notices, analytics.Targets(), null, null, null, tickets);
    }

    private async Task<ReportContent> AgentsAsync(TicketQuery q, CancellationToken ct)
    {
        var a = await GetAgentsHandler.ExecuteAsync(q, analytics, ct);
        var named = a.Agents.Where(x => x.AgentId is not null).ToList();
        var highlights = new List<string> { $"{named.Count} agentes atendieron {a.Meta.Population} tickets en el período." };
        if (named.MaxBy(x => x.Assigned) is { } top && a.Meta.Population > 0)
            highlights.Add($"{top.Name} concentra {Math.Round(100.0 * top.Assigned / a.Meta.Population)} % de la carga ({top.Assigned} tickets).");
        if (a.Agents.FirstOrDefault(x => x.AgentId is null) is { } none && none.Assigned > 0)
            highlights.Add($"{none.Assigned} tickets no tienen agente responsable y no se atribuyen a nadie.");
        highlights.Add("La comparación entre agentes depende del tipo y la complejidad de sus casos: úsela para equilibrar la carga, no como ranking.");

        return new ReportContent(
            await HeaderAsync("agentes", "Rendimiento y carga del equipo", "Indicadores por agente y totales del equipo en el período.", a.Meta, q, ct),
            KpiLines(a.Team, null, null),
            highlights, a.Notices, analytics.Targets(), null, a, null, []);
    }

    private async Task<ReportHeader> HeaderAsync(string kind, string title, string purpose, Meta meta, TicketQuery q, CancellationToken ct) =>
        new(kind, title, purpose, meta, await analytics.DescribeFiltersAsync(q, ct), analytics.Clock.LocalNow);

    // ---------- Indicadores ----------


    private List<KpiLine> KpiLines(Indicators cur, Indicators? prev, Volume? volume)
    {
        var g = _o.Goals;
        var lines = new List<KpiLine>
        {
            Rate("Eficacia", "Eficacia", cur.Eficacia.Percent, prev?.Eficacia.Percent, g.EficaciaPercent, Definitions[0].Formula),
            Rate("Efectividad", "Efectividad", cur.Efectividad.Percent, prev?.Efectividad.Percent, g.EfectividadPercent, Definitions[1].Formula),
            Time("Tiempo de resolución (mediana)", cur.ResolutionTime.MedianSeconds, prev?.ResolutionTime.MedianSeconds, Definitions[2].Formula),
            Time("Resolución, 9 de cada 10", cur.ResolutionTime.P90Seconds, prev?.ResolutionTime.P90Seconds, Definitions[3].Formula),
            Time("Primera atención (mediana)", cur.FirstAttentionTime.MedianSeconds, prev?.FirstAttentionTime.MedianSeconds, Definitions[4].Formula),
            new("Reapertura", "Calidad", KpiUnit.Percent, cur.ReopenRate.Percent, prev?.ReopenRate.Percent, null,
                Trend(cur.ReopenRate.Percent, prev?.ReopenRate.Percent, lowerIsBetter: true), Definitions[5].Formula),
        };
        if (volume is not null)
        {
            lines.Add(new("Tickets recibidos", "Volumen", KpiUnit.Count, volume.Received, volume.ReceivedPrevious, null, KpiStatus.None, "Tickets creados en el período (sin fusionados ni eliminados)."));
            lines.Add(new("Tickets resueltos", "Volumen", KpiUnit.Count, volume.Resolved, volume.ResolvedPrevious, null, KpiStatus.None, "De los recibidos, cuántos están resueltos hoy."));
        }
        return lines;

        KpiLine Rate(string name, string dim, double? c, double? p, double goal, string formula) =>
            new(name, dim, KpiUnit.Percent, c, p, goal, c is not { } v ? KpiStatus.NoData : v >= goal ? KpiStatus.Meets : v >= goal - g.NearMarginPoints ? KpiStatus.Near : KpiStatus.Below, formula);

        static KpiLine Time(string name, double? c, double? p, string formula) =>
            new(name, "Eficiencia", KpiUnit.Duration, c, p, null, Trend(c, p, lowerIsBetter: true), formula);
    }

    private static KpiStatus Trend(double? c, double? p, bool lowerIsBetter)
    {
        if (c is not { } cur) return KpiStatus.NoData;
        if (p is not { } prev) return KpiStatus.None;
        if (Math.Abs(cur - prev) < 0.05) return KpiStatus.Same;
        return (cur < prev) == lowerIsBetter ? KpiStatus.Better : KpiStatus.Worse;
    }

    /// <summary>Hallazgos en lenguaje simple: qué pasó y qué lo explica, no solo la cifra.</summary>
    private List<string> ServiceHighlights(OverviewResponse o)
    {
        var list = new List<string>();
        var i = o.Indicators;
        if (o.Volume.Received == 0) return ["No se recibieron tickets en el período con los filtros aplicados."];

        list.Add($"Se recibieron {o.Volume.Received} tickets y {o.Volume.Resolved} ya están resueltos (eficacia {Pct(i.Eficacia.Percent)}, meta {_o.Goals.EficaciaPercent} %).");

        var evaluated = o.Outcomes.Where(x => x.Key is not ("pending" or "excluded")).ToList();
        var failure = evaluated.Where(x => x.Key != "onTime" && x.Count > 0).MaxBy(x => x.Count);
        if (i.Efectividad.Percent is { } ef)
            list.Add($"La efectividad es {Pct(ef)} (meta {_o.Goals.EfectividadPercent} %)" + (failure is null ? "." : $"; la principal causa de incumplimiento es «{failure.Label.ToLowerInvariant()}» con {failure.Count} tickets."));

        if (i.ResolutionTime.MedianSeconds is { } med)
            list.Add($"La mitad de los tickets se resolvió en menos de {Duration(med)} y 9 de cada 10 en menos de {Duration(i.ResolutionTime.P90Seconds)}.");

        if (o.ResolutionByCategory.Where(c => c.Sample >= 3 && c.MedianSeconds is not null).MaxBy(c => c.MedianSeconds) is { } slow)
            list.Add($"La categoría más lenta con casos suficientes es «{slow.Label}» ({Duration(slow.MedianSeconds)} de mediana, {slow.Sample} tickets).");

        if (o.Current.Overdue > 0)
            list.Add($"Hoy hay {o.Current.Overdue} tickets abiertos fuera de su objetivo y {o.Current.Unassigned} sin agente asignado.");

        var excluded = o.Outcomes.FirstOrDefault(x => x.Key == "excluded")?.Count ?? 0;
        if (excluded > 0)
            list.Add($"{excluded} tickets se cerraron con acción masiva: cuentan para eficacia pero no para tiempos ni efectividad.");
        return list;
    }
}
