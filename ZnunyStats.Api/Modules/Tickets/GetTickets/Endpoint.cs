using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Tickets.GetTickets;

public static class GetTicketsEndpoint
{
    public static void MapGetTickets(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithSummary("Tickets que explican las cifras. view=attention lista abiertos, atrasados primero.");
    }

    private static async Task<IResult> Handle(
        [AsParameters] TicketQuery query,
        TicketAnalytics analytics,
        CancellationToken cancellationToken,
        string? view,
        int page = 1,
        int pageSize = 50)
    {
        return TypedResults.Ok(await GetTicketsHandler.ExecuteAsync(query, view, page, pageSize, analytics, cancellationToken));
    }
}
