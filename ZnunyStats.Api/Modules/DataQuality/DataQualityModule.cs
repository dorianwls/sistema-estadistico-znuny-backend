using ZnunyStats.Api.Modules.DataQuality.GetDataQuality;

namespace ZnunyStats.Api.Modules.DataQuality;

public static class DataQualityModule
{
    public static void MapDataQualityModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/data-quality").WithTags("DataQuality");

        group.MapGetDataQuality();
    }
}
