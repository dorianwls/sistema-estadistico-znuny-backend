using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Agents.GetAgents;

/// <summary>Indicadores por agente. Lo usan el endpoint y el reporte de agentes.</summary>
public static class GetAgentsHandler
{
    public static async Task<AgentsResponse> ExecuteAsync(TicketQuery q, TicketAnalytics a, CancellationToken ct)
    {
        var snap = await a.GetSnapshotAsync(ct);
        var period = a.ResolvePeriod(q);
        var current = a.Population(snap, q, period);
        var now = a.NowUtc;

        // Orden por volumen a cargo, no por "puntaje": la comparación entre agentes
        // depende de la complejidad y el tipo de casos de cada uno.
        var agents = current
            .GroupBy(t => (t.AgentId, t.AgentName))
            .Select(g =>
            {
                var tickets = g.ToList();
                return new AgentScore(
                    g.Key.AgentId,
                    g.Key.AgentName,
                    tickets.Count,
                    tickets.Count(t => t.IsResolved),
                    tickets.Count(t => t.IsOpen),
                    tickets.Count(t => t.IsOverdue(now)),
                    tickets.Count(t => t.BulkClosed),
                    tickets.GroupBy(t => t.Unit).OrderByDescending(u => u.Count()).Select(u => u.Key).ToList(),
                    a.BuildIndicators(tickets));
            })
            .OrderBy(x => x.AgentId is null)
            .ThenByDescending(x => x.Assigned)
            .ToList();

        return new AgentsResponse(a.Meta(snap, period, current.Count), a.BuildIndicators(current), agents, a.Notices(snap, current));
    }
}
