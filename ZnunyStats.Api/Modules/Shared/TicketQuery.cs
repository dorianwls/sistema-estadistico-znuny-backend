namespace ZnunyStats.Api.Modules.Shared;

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
