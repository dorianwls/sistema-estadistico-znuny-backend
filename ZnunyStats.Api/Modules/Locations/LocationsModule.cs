using ZnunyStats.Api.Modules.Locations.GetLocations;

namespace ZnunyStats.Api.Modules.Locations;

public static class LocationsModule
{
    public static void MapLocationsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/locations").WithTags("Locations");

        group.MapGetLocations();
    }
}
