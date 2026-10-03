using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Locations.GetLocations;

public static class GetLocationsEndpoint
{
    public static void MapGetLocations(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithSummary("Módulo Mapa institucional: indicadores por recinto.");
    }

    private static async Task<IResult> Handle(
        [AsParameters] TicketQuery query,
        TicketAnalytics analytics,
        CancellationToken cancellationToken)
    {
        var snap = await analytics.GetSnapshotAsync(cancellationToken);
        var period = analytics.ResolvePeriod(query);
        // Todas las ubicaciones a la vez: el filtro de ubicación no aplica aquí.
        var current = analytics.Population(snap, query with { Location = null }, period);
        var now = analytics.NowUtc;

        var locations = analytics.Options.Locations
            .Select(l =>
            {
                var tickets = current.Where(t => t.LocationKey == l.Key).ToList();
                return new LocationSummary(
                    l.Key,
                    l.Name,
                    l.Latitude,
                    l.Longitude,
                    tickets.Count,
                    tickets.Count(t => t.IsOpen),
                    tickets.Count(t => t.IsOverdue(now)),
                    tickets.Count(t => t.IsResolved),
                    analytics.BuildIndicators(tickets),
                    TicketAnalytics.CountBy(tickets, t => t.Unit));
            })
            .ToList();

        return TypedResults.Ok(new LocationsResponse(analytics.Meta(snap, period, current.Count), locations));
    }
}
