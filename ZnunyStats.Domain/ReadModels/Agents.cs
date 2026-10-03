using ZnunyStats.Domain.Metrics;

namespace ZnunyStats.Domain.ReadModels;

// Modelos de lectura que devuelve la API y consumen los reportes.
// Duraciones en segundos, porcentajes de 0 a 100, null = no calculable.

public sealed record AgentScore(
    int? AgentId,
    string Name,
    int Assigned,
    int Resolved,
    int Open,
    int Overdue,
    int BulkClosed,
    IReadOnlyList<string> Units,
    Indicators Indicators);

public sealed record AgentsResponse(Meta Meta, Indicators Team, IReadOnlyList<AgentScore> Agents, IReadOnlyList<Notice> Notices);
