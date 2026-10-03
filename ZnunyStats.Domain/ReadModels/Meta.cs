namespace ZnunyStats.Domain.ReadModels;

// Modelos de lectura que devuelve la API y consumen los reportes.
// Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

public sealed record Meta(
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    string TimeZone,
    DateTimeOffset DataLoadedAt,
    int Population);

public sealed record Notice(string Code, string Severity, string Message);

public sealed record TargetInfo(string Priority, double Hours);

public sealed record CountItem(string Label, int Count, double Percent);
