using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Domain.Entities;

namespace ZnunyStats.Api.Modules.Catalogs.GetCatalogs;

public static class GetCatalogsEndpoint
{
    public static void MapGetCatalogs(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithSummary("Opciones válidas para los filtros, rango de datos y objetivos por prioridad.");
    }

    private static async Task<IResult> Handle(
        TicketAnalytics analytics,
        CancellationToken cancellationToken)
    {
        var snap = await analytics.GetSnapshotAsync(cancellationToken);
        var t = snap.Tickets.Where(x => x.IsCountable).ToList();
        var period = analytics.ResolvePeriod(new TicketQuery(null, null, null, null, null, null, null, null, null));

        static IReadOnlyList<Option> Distinct(IEnumerable<string> values) =>
            values.Distinct().Order(StringComparer.CurrentCulture).Select(v => new Option(v, v)).ToList();

        return TypedResults.Ok(new CatalogsResponse(
            Distinct(t.Select(x => x.Area)),
            Distinct(t.Select(x => x.Unit)),
            Distinct(t.Select(x => x.Category)),
            t.GroupBy(x => x.PriorityId).OrderByDescending(g => g.Key)
                .Select(g => new Option(g.Key.ToString(), g.First().Priority)).ToList(),
            TicketStatus.All.Select(s => new Option(s, s)).ToList(),
            t.Where(x => x.AgentId is not null).GroupBy(x => x.AgentId!.Value)
                .Select(g => new Option(g.Key.ToString(), g.First().AgentName)).OrderBy(o => o.Label).ToList(),
            analytics.Options.Locations.Select(l => new Option(l.Key, l.Name)).ToList(),
            t.Count > 0 ? analytics.Clock.LocalDate(t.Min(x => x.CreatedAt)) : null,
            t.Count > 0 ? analytics.Clock.LocalDate(t.Max(x => x.CreatedAt)) : null,
            period.From,
            period.To,
            analytics.Targets(),
            analytics.Options.ReopenWindowDays));
    }
}
