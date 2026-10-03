namespace ZnunyStats.Domain.ReadModels;

// Modelos de lectura que devuelve la API y consumen los reportes.
// Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

public sealed record QualityCheck(string Code, string Severity, string Title, string Detail, int? Affected, int? OutOf, IReadOnlyList<string> Affects);

public sealed record DataQualityResponse(Meta Meta, IReadOnlyList<QualityCheck> Checks);
