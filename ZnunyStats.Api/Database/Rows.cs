namespace ZnunyStats.Api.Database;

// Filas tal como las devuelven las consultas de ZnunyQueries (mapeadas por Dapper).

public sealed class TicketFact
{
    public long TicketId { get; init; }
    public string TicketNumber { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string QueueName { get; init; } = "";
    public int PriorityId { get; init; }
    public string PriorityName { get; init; } = "";
    public string StateName { get; init; } = "";
    public string StateType { get; init; } = "";
    public int CurrentOwnerId { get; init; }
    public string? CustomerArea { get; init; }
    public bool CreatedByAgent { get; init; }
    public DateTime? FirstAttentionAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public int? ResolvedById { get; init; }
    public int? ResolvedOwnerId { get; init; }
    public bool BulkClosed { get; init; }
    public DateTime? ReopenedAt { get; init; }
    public int Transfers { get; init; }
    public int AgentReplies { get; init; }
}

public sealed class UserRow
{
    public int Id { get; init; }
    public string Login { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}

public sealed class SourceCapabilities
{
    public int ActiveSlas { get; init; }
    public int TimeAccountingEntries { get; init; }
}

public sealed class HistoryRow
{
    public long Id { get; init; }
    public DateTime At { get; init; }
    public string Type { get; init; } = "";
    public string Detail { get; init; } = "";
    public string State { get; init; } = "";
    public string Queue { get; init; } = "";
    public int ActorId { get; init; }
}
