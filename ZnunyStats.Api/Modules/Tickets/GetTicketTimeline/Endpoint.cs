using ZnunyStats.Api.Database;
using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Tickets.GetTicketTimeline;

public static class GetTicketTimelineEndpoint
{
    public static void MapGetTicketTimeline(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:long}/timeline", Handle)
            .WithSummary("Historial legible de un ticket.");
    }

    private static async Task<IResult> Handle(
        long id,
        TicketAnalytics analytics,
        ZnunyQueries queries,
        CancellationToken cancellationToken)
    {
        var snap = await analytics.GetSnapshotAsync(cancellationToken);
        var ticket = snap.Tickets.FirstOrDefault(t => t.Id == id);
        if (ticket is null)
        {
            return TypedResults.NotFound();
        }

        var history = await queries.GetTicketHistoryAsync(id, analytics.Options.SourceTimeZone, cancellationToken);
        var events = history
            .Select(h => new TimelineEvent(
                analytics.Clock.ToLocal(h.At),
                h.Type,
                HistoryText.Describe(h.Type, h.Detail),
                snap.AgentNames.TryGetValue(h.ActorId, out var name) ? name : "Sistema",
                h.State,
                h.Queue))
            .ToList();

        return TypedResults.Ok(new TicketTimeline(analytics.ToRow(ticket), events));
    }
}
