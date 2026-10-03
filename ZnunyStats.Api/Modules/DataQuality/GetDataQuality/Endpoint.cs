using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.DataQuality.GetDataQuality;

public static class GetDataQualityEndpoint
{
    public static void MapGetDataQuality(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithSummary("Qué limita la confianza en las cifras.");
    }

    private static async Task<IResult> Handle(
        [AsParameters] TicketQuery query,
        TicketAnalytics analytics,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await GetDataQualityHandler.ExecuteAsync(query, analytics, cancellationToken));
    }
}
