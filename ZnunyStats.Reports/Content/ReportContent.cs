using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Reports.Content;

public enum KpiUnit { Percent, Duration, Count }

/// <summary>Cumple/Cerca/Bajo comparan contra la meta; Mejor/Peor/Igual contra el período anterior (cuando no hay meta).</summary>
public enum KpiStatus { Meets, Near, Below, Better, Worse, Same, NoData, None }

/// <summary>Una fila del resumen ejecutivo: valor actual, anterior, meta y estado, con su fórmula.</summary>
public sealed record KpiLine(string Name, string Dimension, KpiUnit Unit, double? Current, double? Previous, double? Goal, KpiStatus Status, string Formula)
{
    /// <summary>Variación en la unidad del indicador (puntos para %, segundos para tiempos).</summary>
    public double? Variation => Current is { } c && Previous is { } p ? c - p : null;
}

public sealed record ReportHeader(string Kind, string Title, string Purpose, Meta Meta, IReadOnlyList<(string Label, string Value)> Filters, DateTimeOffset GeneratedAt);

/// <summary>
/// Todo lo que lleva un reporte, ya calculado. Excel y PDF son dos presentaciones del mismo contenido:
/// así una cifra nunca difiere entre formatos.
/// </summary>
public sealed record ReportContent(
    ReportHeader Header,
    IReadOnlyList<KpiLine> Kpis,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<Notice> Notices,
    IReadOnlyList<TargetInfo> Targets,
    OverviewResponse? Overview,
    AgentsResponse? Agents,
    DataQualityResponse? Quality,
    IReadOnlyList<TicketRow> Tickets);
