namespace ZnunyStats.Domain.Metrics;

public sealed record Indicators(
    RateKpi Eficacia,
    RateKpi Efectividad,
    DurationKpi ResolutionTime,
    DurationKpi FirstAttentionTime,
    RateKpi ReopenRate);
