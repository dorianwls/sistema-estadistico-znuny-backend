namespace ZnunyStats.Api.Analytics;

// Contratos JSON de la API. Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

/// <summary>Filtros comunes (query string). Fechas locales e inclusivas; la población son los tickets creados en ese rango.</summary>
public sealed record TicketQuery(
    DateOnly? From,
    DateOnly? To,
    string? Area,
    string? Unit,
    string? Category,
    string? Status,
    int? AgentId,
    int? PriorityId,
    string? Location);

public sealed record Meta(
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    string TimeZone,
    DateTimeOffset DataLoadedAt,
    int Population);

public sealed record Indicators(
    RateKpi Eficacia,
    RateKpi Efectividad,
    DurationKpi ResolutionTime,
    DurationKpi FirstAttentionTime,
    RateKpi ReopenRate);

public sealed record Notice(string Code, string Severity, string Message);

public sealed record CountItem(string Label, int Count, double Percent);

public sealed record DurationItem(string Label, double? MedianSeconds, int Sample);

public sealed record TrendPoint(string Label, int Current, int Previous);

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
    IReadOnlyList<Notice> Notices);

public sealed record AgentScore(
    int? AgentId,
    string Name,
    int Assigned,
    int Resolved,
    int Open,
    int Overdue,
    int BulkClosed,
    IReadOnlyList<string> Units,
    Indicators Indicators);

public sealed record AgentsResponse(Meta Meta, Indicators Team, IReadOnlyList<AgentScore> Agents, IReadOnlyList<Notice> Notices);

public sealed record TicketRow(
    long Id,
    string Number,
    string Title,
    DateTimeOffset CreatedAt,
    string Area,
    string Unit,
    string Category,
    string Location,
    string Priority,
    string Status,
    string Agent,
    double? FirstAttentionSeconds,
    double? ResolutionSeconds,
    double TargetSeconds,
    double AgeSeconds,
    bool Overdue,
    bool BulkClosed,
    bool Reopened);

public sealed record TicketPage(int Total, int Page, int PageSize, IReadOnlyList<TicketRow> Items);

public sealed record TimelineEvent(DateTimeOffset At, string Kind, string Description, string Actor, string State, string Queue);

public sealed record TicketTimeline(TicketRow Ticket, IReadOnlyList<TimelineEvent> Events);

public sealed record LocationSummary(
    string Key,
    string Name,
    double? Latitude,
    double? Longitude,
    int Total,
    int Open,
    int Overdue,
    int Resolved,
    Indicators Indicators,
    IReadOnlyList<CountItem> ByUnit);

public sealed record LocationsResponse(Meta Meta, IReadOnlyList<LocationSummary> Locations);

public sealed record Option(string Value, string Label);

public sealed record Catalogs(
    IReadOnlyList<Option> Areas,
    IReadOnlyList<Option> Units,
    IReadOnlyList<Option> Categories,
    IReadOnlyList<Option> Priorities,
    IReadOnlyList<Option> Statuses,
    IReadOnlyList<Option> Agents,
    IReadOnlyList<Option> Locations,
    DateOnly? FirstTicketDate,
    DateOnly? LastTicketDate,
    DateOnly DefaultFrom,
    DateOnly DefaultTo,
    IReadOnlyList<TargetInfo> Targets,
    int ReopenWindowDays);

public sealed record TargetInfo(string Priority, double Hours);

public sealed record QualityCheck(string Code, string Severity, string Title, string Detail, int? Affected, int? OutOf, IReadOnlyList<string> Affects);

public sealed record DataQualityResponse(Meta Meta, IReadOnlyList<QualityCheck> Checks);
