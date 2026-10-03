namespace ZnunyStats.Api.Modules.Shared;

/// <summary>Período de análisis en fechas locales, inclusivo, y el período anterior de igual duración para comparar.</summary>
public sealed record Period(DateOnly From, DateOnly To)
{
    public const int DefaultDays = 30;

    /// <summary>Diferencia máxima entre <c>from</c> y <c>to</c>: un año.</summary>
    public const int MaxSpanDays = 366;

    public int Days => To.DayNumber - From.DayNumber + 1;
    public DateOnly PreviousTo => From.AddDays(-1);
    public DateOnly PreviousFrom => From.AddDays(-Days);

    /// <summary>Por defecto: los últimos 30 días hasta hoy. Los límites se validan en <see cref="TicketQueryValidator"/>.</summary>
    public static Period Resolve(TicketQuery q, DateOnly today)
    {
        var to = q.To ?? today;
        var from = q.From ?? to.AddDays(-(DefaultDays - 1));
        return new Period(from, to);
    }
}
