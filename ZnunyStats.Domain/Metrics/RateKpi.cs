namespace ZnunyStats.Domain.Metrics;

/// <summary>
/// Una proporción. <c>Percent</c> es null cuando no hay casos evaluables (nunca se reemplaza por cero).
/// Pending: casos cuyo resultado aún no se puede juzgar. Excluded: casos fuera del cálculo, con motivo documentado.
/// </summary>
public sealed record RateKpi(double? Percent, int Numerator, int Denominator, int Pending, int Excluded);
