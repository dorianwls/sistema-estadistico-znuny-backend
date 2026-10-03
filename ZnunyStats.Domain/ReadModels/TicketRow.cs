namespace ZnunyStats.Domain.ReadModels;

// Modelos de lectura que devuelve la API y consumen los reportes.
// Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

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
