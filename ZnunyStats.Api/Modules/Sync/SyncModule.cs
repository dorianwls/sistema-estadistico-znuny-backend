using ZnunyStats.Api.Modules.Sync.RefreshData;

namespace ZnunyStats.Api.Modules.Sync;

public static class SyncModule
{
    public static void MapSyncModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/refresh").WithTags("Sync");

        group.MapRefreshData();
    }
}
