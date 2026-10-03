using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Modules.Sync.RefreshData;

public static class RefreshDataEndpoint
{
    public static void MapRefreshData(this RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithSummary("Descarta la caché para releer Znuny en la próxima consulta (no escribe en Znuny).");
    }

    private static IResult Handle(TicketStore store)
    {
        store.Invalidate();
        return TypedResults.NoContent();
    }
}
