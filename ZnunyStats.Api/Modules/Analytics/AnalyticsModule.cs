using ZnunyStats.Api.Modules.Analytics.GetOverview;

namespace ZnunyStats.Api.Modules.Analytics;

public static class AnalyticsModule
{
    public static void MapAnalyticsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics").WithTags("Analytics");

        group.MapGetOverview();
    }
}
