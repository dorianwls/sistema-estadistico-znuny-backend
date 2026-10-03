using Microsoft.Extensions.Options;

namespace ZnunyStats.Api.Common;

/// <summary>
/// Reloj de negocio. Znuny guarda las fechas en UTC; los períodos se definen y se presentan en la zona configurada
/// (America/Managua por defecto). Toda conversión de fechas pasa por aquí.
/// </summary>
public sealed class ZnunyClock(IOptions<StatsOptions> options, TimeProvider time)
{
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public string TimeZoneId => options.Value.TimeZone;

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(time.GetUtcNow(), _tz);

    public DateOnly Today => LocalDate(UtcNow);

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _tz));

    public DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _tz);

    public DateTimeOffset ToLocal(DateTime utc)
    {
        var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(u, _tz), _tz.GetUtcOffset(u));
    }
}
