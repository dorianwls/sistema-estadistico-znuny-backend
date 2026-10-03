using Swashbuckle.AspNetCore.SwaggerUI;

using ZnunyStats.Api.Common.Validation;
using ZnunyStats.Api.Modules.Agents;
using ZnunyStats.Api.Modules.Analytics;
using ZnunyStats.Api.Modules.Catalogs;
using ZnunyStats.Api.Modules.DataQuality;
using ZnunyStats.Api.Modules.Locations;
using ZnunyStats.Api.Modules.Reports;
using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Api.Modules.Sync;
using ZnunyStats.Api.Modules.Tickets;

namespace ZnunyStats.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            options.RoutePrefix = string.Empty;
            options.DocExpansion(DocExpansion.None);
        });

        return app;
    }

    /// <summary>
    /// API REST de solo lectura bajo /api/v1. Los endpoints de datos aceptan los filtros de <see cref="TicketQuery"/>
    /// (from, to, area, unit, category, status, agentId, priorityId, location), validados en un solo lugar.
    /// </summary>
    public static void MapEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1")
            .AddEndpointFilter<ValidationFilter<TicketQuery>>();

        api.MapCatalogsModule();
        api.MapAnalyticsModule();
        api.MapAgentsModule();
        api.MapTicketsModule();
        api.MapLocationsModule();
        api.MapDataQualityModule();
        api.MapReportsModule();
        api.MapSyncModule();
    }
}
