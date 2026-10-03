using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Analytics.GetOverview;

public static class GetOverviewEndpoint
{
    public static void MapGetOverview(this RouteGroupBuilder group)
    {
        group.MapGet("/overview", Handle)
            .WithSummary("Módulo Análisis: indicadores, tendencia y distribuciones.");
    }

    private static async Task<IResult> Handle(
        [AsParameters] TicketQuery query,
        TicketAnalytics analytics,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await GetOverviewHandler.ExecuteAsync(query, analytics, cancellationToken));
    }
}
