namespace ZnunyStats.Domain.Metrics;

/// <summary>Una duración en segundos. Se reporta mediana (valor típico) y p90 (casos lentos).</summary>
public sealed record DurationKpi(double? MedianSeconds, double? P90Seconds, double? AverageSeconds, int Sample, int Excluded);
