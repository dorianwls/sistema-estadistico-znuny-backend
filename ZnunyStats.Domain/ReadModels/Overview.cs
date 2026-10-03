using ZnunyStats.Domain.Metrics;

namespace ZnunyStats.Domain.ReadModels;

// Modelos de lectura que devuelve la API y consumen los reportes.
// Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

public sealed record DurationItem(string Label, double? MedianSeconds, int Sample);

public sealed record TrendPoint(string Label, int Current, int Previous);

/// <summary>Un tramo de la serie temporal: tickets creados en el tramo, cómo terminaron y sus indicadores.</summary>
public sealed record SeriesPoint(string Label, int Received, int Resolved, int Unsuccessful, int Open, double? Eficacia, double? Efectividad, double? MedianResolutionSeconds);

/// <summary>Resultado de efectividad: Key es el código estable (onTime, late, reopened, unsuccessful, overdue, pending, excluded).</summary>
public sealed record OutcomeItem(string Key, string Label, int Count);

/// <summary>Cumplimiento del objetivo de resolución por prioridad.</summary>
public sealed record PriorityCompliance(string Label, double TargetHours, int Evaluated, int OnTime, double? Percent, double? MedianSeconds);

/// <summary>Los tres indicadores de un grupo (equipo de TI o área solicitante).</summary>
public sealed record GroupScore(string Label, int Received, double? Eficacia, double? Efectividad, double? MedianResolutionSeconds);

/// <summary>Foto del momento actual (no depende del período, sí del resto de filtros).</summary>
public sealed record CurrentState(int Open, int Overdue, int Unassigned);

/// <summary>Volumen de la cohorte: creados en el período y, de ellos, cuántos están resueltos.</summary>
public sealed record Volume(int Received, int ReceivedPrevious, int Resolved, int ResolvedPrevious);

public sealed record OverviewResponse(
    Meta Meta,
    CurrentState Current,
    Volume Volume,
    Indicators Indicators,
    Indicators PreviousIndicators,
    string TrendBucket,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByArea,
    IReadOnlyList<CountItem> ByUnit,
    IReadOnlyList<DurationItem> ResolutionByCategory,
    IReadOnlyList<SeriesPoint> Series,
    IReadOnlyList<OutcomeItem> Outcomes,
    IReadOnlyList<CountItem> ResolutionDistribution,
    IReadOnlyList<PriorityCompliance> ByPriority,
    IReadOnlyList<GroupScore> UnitScores,
    IReadOnlyList<GroupScore> AreaScores,
    IReadOnlyList<Notice> Notices);
