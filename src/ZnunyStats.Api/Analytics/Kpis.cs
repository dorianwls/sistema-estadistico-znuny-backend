namespace ZnunyStats.Api.Analytics;

/// <summary>
/// Una proporción. <c>Percent</c> es null cuando no hay casos evaluables (nunca se reemplaza por cero).
/// Pending: casos cuyo resultado aún no se puede juzgar. Excluded: casos fuera del cálculo, con motivo documentado.
/// </summary>
public sealed record RateKpi(double? Percent, int Numerator, int Denominator, int Pending, int Excluded);

/// <summary>Una duración en segundos. Se reporta mediana (valor típico) y p90 (casos lentos).</summary>
public sealed record DurationKpi(double? MedianSeconds, double? P90Seconds, double? AverageSeconds, int Sample, int Excluded);

/// <summary>
/// Definiciones de los indicadores. Todas reciben la población ya filtrada
/// (tickets creados en el período y a cargo del agente o equipo evaluado).
///
///   Eficacia    → ¿Se logró el resultado?      tickets resueltos / tickets a cargo
///   Eficiencia  → ¿Con cuánto tiempo?          mediana de tiempo hasta resolución y hasta primera atención
///   Efectividad → ¿Se logró bien y a tiempo?   resueltos dentro del objetivo y sin reapertura / tickets con resultado conocido
/// </summary>
public static class Kpis
{
    /// <summary>Eficacia: proporción de tickets a cargo que hoy están resueltos con éxito.</summary>
    public static RateKpi Eficacia(IReadOnlyCollection<TicketRecord> tickets)
    {
        var population = tickets.Where(t => t.IsCountable).ToList();
        return Rate(population.Count(t => t.IsResolved), population.Count, pending: 0, excluded: tickets.Count - population.Count);
    }

    /// <summary>
    /// Efectividad: de los tickets cuyo resultado ya se conoce, cuántos se resolvieron
    /// dentro del objetivo de su prioridad y no se reabrieron.
    /// - Resultado conocido: ya se resolvió (alguna vez), se cerró sin éxito, o sigue abierto pasado su objetivo.
    /// - Pendiente: abierto y todavía dentro de su objetivo.
    /// - Excluido: cerrado con acción masiva (su duración no refleja el trabajo real).
    /// </summary>
    public static RateKpi Efectividad(IReadOnlyCollection<TicketRecord> tickets, DateTime nowUtc, int reopenWindowDays)
    {
        int success = 0, evaluated = 0, pending = 0, excluded = 0;
        foreach (var t in tickets.Where(t => t.IsCountable))
        {
            if (t.BulkClosed) { excluded++; continue; }

            var outcomeKnown = t.ResolvedAt is not null
                || t.Status == TicketStatus.ClosedUnsuccessful
                || t.IsOverdue(nowUtc);
            if (!outcomeKnown) { pending++; continue; }

            evaluated++;
            if (t.IsResolved && t.ResolvedWithinTarget && !t.ReopenedWithin(reopenWindowDays))
                success++;
        }
        return Rate(success, evaluated, pending, excluded);
    }

    /// <summary>Eficiencia (1): tiempo desde la creación hasta el primer cierre exitoso. Excluye cierres masivos.</summary>
    public static DurationKpi ResolutionTime(IReadOnlyCollection<TicketRecord> tickets)
    {
        var resolved = tickets.Where(t => t.IsCountable && t.ResolvedAt is not null).ToList();
        var valid = resolved.Where(t => !t.BulkClosed).Select(t => t.ResolutionSeconds!.Value);
        return Duration(valid, excluded: resolved.Count(t => t.BulkClosed));
    }

    /// <summary>
    /// Eficiencia (2): tiempo desde la creación hasta la primera acción de un agente.
    /// Excluye cierres masivos y tickets abiertos por el propio agente (no hubo espera que medir).
    /// </summary>
    public static DurationKpi FirstAttentionTime(IReadOnlyCollection<TicketRecord> tickets)
    {
        var attended = tickets.Where(t => t.IsCountable && t.FirstAttentionAt is not null).ToList();
        bool Measurable(TicketRecord t) => !t.BulkClosed && !t.CreatedByAgent;
        var valid = attended.Where(Measurable).Select(t => t.FirstAttentionSeconds!.Value);
        return Duration(valid, excluded: attended.Count(t => !Measurable(t)));
    }

    /// <summary>
    /// Reapertura: tickets resueltos que volvieron a un estado activo dentro de la ventana.
    /// Solo se evalúan los resueltos hace más de la ventana; los recientes quedan pendientes.
    /// </summary>
    public static RateKpi ReopenRate(IReadOnlyCollection<TicketRecord> tickets, DateTime nowUtc, int windowDays)
    {
        var resolved = tickets.Where(t => t.IsCountable && t.ResolvedAt is not null).ToList();
        var mature = resolved.Where(t => nowUtc - t.ResolvedAt!.Value >= TimeSpan.FromDays(windowDays)).ToList();
        return Rate(mature.Count(t => t.ReopenedWithin(windowDays)), mature.Count, pending: resolved.Count - mature.Count, excluded: 0);
    }

    public static RateKpi Rate(int numerator, int denominator, int pending, int excluded) =>
        new(denominator == 0 ? null : Math.Round(100.0 * numerator / denominator, 1), numerator, denominator, pending, excluded);

    public static DurationKpi Duration(IEnumerable<double> seconds, int excluded)
    {
        var sorted = seconds.Order().ToArray();
        if (sorted.Length == 0)
            return new DurationKpi(null, null, null, 0, excluded);

        return new DurationKpi(
            Math.Round(Percentile(sorted, 0.5)),
            Math.Round(Percentile(sorted, 0.9)),
            Math.Round(sorted.Average()),
            sorted.Length,
            excluded);
    }

    /// <summary>Percentil con interpolación lineal (equivalente a percentile_cont de PostgreSQL).</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var position = p * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
