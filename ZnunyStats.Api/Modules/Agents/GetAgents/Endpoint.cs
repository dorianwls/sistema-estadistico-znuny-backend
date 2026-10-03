using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Agents.GetAgents;

public static class GetAgentsEndpoint
{
    public static void MapGetAgents(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithSummary("Módulo Agentes: eficacia, eficiencia y efectividad por agente.");
    }

    private static async Task<IResult> Handle(
        [AsParameters] TicketQuery query,
        TicketAnalytics analytics,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await GetAgentsHandler.ExecuteAsync(query, analytics, cancellationToken));
    }
}
