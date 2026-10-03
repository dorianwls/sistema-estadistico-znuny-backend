using ZnunyStats.Api.Modules.Catalogs.GetCatalogs;

namespace ZnunyStats.Api.Modules.Catalogs;

public static class CatalogsModule
{
    public static void MapCatalogsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/catalogs").WithTags("Catalogs");

        group.MapGetCatalogs();
    }
}
