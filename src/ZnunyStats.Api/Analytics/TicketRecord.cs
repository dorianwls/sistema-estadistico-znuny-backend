namespace ZnunyStats.Api.Analytics;

/// <summary>Estados simplificados que ve el usuario.</summary>
public static class TicketStatus
{
    public const string New = "Nuevo";
    public const string Open = "Abierto";
    public const string Pending = "Pendiente";
    public const string Resolved = "Resuelto";
    public const string ClosedUnsuccessful = "Cerrado sin éxito";
    public const string Merged = "Fusionado";
    public const string Removed = "Eliminado";

    public static readonly string[] All = [New, Open, Pending, Resolved, ClosedUnsuccessful];
}

/// <summary>
/// Un ticket listo para calcular indicadores: hechos de Znuny + clasificación de negocio.
/// Las fechas están en UTC; la conversión a hora local se hace solo al agrupar o presentar.
/// </summary>
public sealed record TicketRecord
{
    public required long Id { get; init; }
    public required string Number { get; init; }
    public required string Title { get; init; }
    public required DateTime CreatedAt { get; init; }

    /// <summary>Cola completa de Znuny, p. ej. "Unidad de Soporte Técnico RUSB::Ofimática".</summary>
    public required string Queue { get; init; }

    /// <summary>Unidad o equipo de TI: primer nivel de la cola.</summary>
    public required string Unit { get; init; }

    /// <summary>Categoría: segundo nivel de la cola, o "General" si el ticket está en la cola raíz.</summary>
    public required string Category { get; init; }

    public required string LocationKey { get; init; }
    public required int PriorityId { get; init; }
    public required string Priority { get; init; }
    public required string Status { get; init; }
    public required string Area { get; init; }

    /// <summary>Agente responsable: propietario al primer cierre exitoso o, si sigue abierto, propietario actual.</summary>
    public required int? AgentId { get; init; }
    public required string AgentName { get; init; }

    /// <summary>Lo abrió un agente (llamada o atención presencial): la primera atención fue inmediata por definición.</summary>
    public required bool CreatedByAgent { get; init; }

    public required DateTime? FirstAttentionAt { get; init; }
    public required DateTime? ResolvedAt { get; init; }
    public required DateTime? ReopenedAt { get; init; }
    public required bool BulkClosed { get; init; }
    public required int Transfers { get; init; }
    public required int AgentReplies { get; init; }

    /// <summary>Objetivo de resolución según la prioridad.</summary>
    public required TimeSpan Target { get; init; }

    public bool IsOpen => Status is TicketStatus.New or TicketStatus.Open or TicketStatus.Pending;
    public bool IsResolved => Status == TicketStatus.Resolved;

    /// <summary>Fusionados y eliminados no son demanda: quedan fuera de toda población.</summary>
    public bool IsCountable => Status is not (TicketStatus.Merged or TicketStatus.Removed);

    public double? ResolutionSeconds => ResolvedAt is { } at ? (at - CreatedAt).TotalSeconds : null;
    public double? FirstAttentionSeconds => FirstAttentionAt is { } at ? Math.Max(0, (at - CreatedAt).TotalSeconds) : null;

    public bool ReopenedWithin(int days) =>
        ResolvedAt is { } resolved && ReopenedAt is { } reopened && reopened - resolved <= TimeSpan.FromDays(days);

    /// <summary>Abierto y con más antigüedad que su objetivo.</summary>
    public bool IsOverdue(DateTime nowUtc) => IsOpen && nowUtc - CreatedAt > Target;

    public bool ResolvedWithinTarget => ResolutionSeconds is { } s && s <= Target.TotalSeconds;
}
