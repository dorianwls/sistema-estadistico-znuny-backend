using ZnunyStats.Api.Analytics;
using ZnunyStats.Api.Data;
using ZnunyStats.Api.Reports;

namespace ZnunyStats.Api.Endpoints;

/// <summary>
/// API REST de solo lectura bajo /api/v1. Todos los endpoints de datos aceptan los filtros de <see cref="TicketQuery"/>:
/// from, to (yyyy-MM-dd, inclusive), area, unit, category, status, agentId, priorityId, location.
/// </summary>
public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").AddEndpointFilter<InvalidFilterHandler>();

        api.MapGet("/catalogs", (AnalyticsService s, CancellationToken ct) => s.CatalogsAsync(ct))
            .WithSummary("Opciones válidas para los filtros, rango de datos y objetivos por prioridad.");

        api.MapGet("/analytics/overview", ([AsParameters] TicketQuery q, AnalyticsService s, CancellationToken ct) => s.OverviewAsync(q, ct))
            .WithSummary("Módulo Análisis: indicadores, tendencia y distribuciones.");

        api.MapGet("/agents", ([AsParameters] TicketQuery q, AnalyticsService s, CancellationToken ct) => s.AgentsAsync(q, ct))
            .WithSummary("Módulo Agentes: eficacia, eficiencia y efectividad por agente.");

        api.MapGet("/tickets", ([AsParameters] TicketQuery q, AnalyticsService s, CancellationToken ct,
                string? view, int page = 1, int pageSize = 50) => s.TicketsAsync(q, view, page, pageSize, ct))
            .WithSummary("Tickets que explican las cifras. view=attention lista abiertos, atrasados primero.");

        api.MapGet("/tickets/{id:long}/timeline", async (long id, AnalyticsService s, CancellationToken ct) =>
                await s.TimelineAsync(id, ct) is { } timeline ? Results.Ok(timeline) : Results.NotFound())
            .WithSummary("Historial legible de un ticket.");

        api.MapGet("/locations", ([AsParameters] TicketQuery q, AnalyticsService s, CancellationToken ct) => s.LocationsAsync(q, ct))
            .WithSummary("Módulo Mapa institucional: indicadores por recinto.");

        api.MapGet("/data-quality", ([AsParameters] TicketQuery q, AnalyticsService s, CancellationToken ct) => s.DataQualityAsync(q, ct))
            .WithSummary("Qué limita la confianza en las cifras.");

        api.MapGet("/reports/{kind}", async (string kind, [AsParameters] TicketQuery q, ExcelReports reports, CancellationToken ct) =>
                await reports.BuildAsync(kind, q, ct) is { } file
                    ? Results.File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName)
                    : Results.NotFound(new { error = $"Reporte desconocido. Opciones: {string.Join(", ", ExcelReports.Kinds)}" }))
            .WithSummary("Exporta a Excel: servicio, atencion o agentes.");

        api.MapPost("/refresh", (TicketStore store) => { store.Invalidate(); return Results.NoContent(); })
            .WithSummary("Descarta la caché para releer Znuny en la próxima consulta (no escribe en Znuny).");

        app.MapGet("/health", async (ZnunyQueries queries, CancellationToken ct) =>
        {
            try { return Results.Ok(new { status = "ok", database = await queries.PingAsync(ct) }); }
            catch (Exception ex) { return Results.Json(new { status = "error", database = false, error = ex.Message }, statusCode: 503); }
        });
    }

    /// <summary>Convierte errores de validación de filtros (p. ej. from &gt; to) en 400 con mensaje claro.</summary>
    private sealed class InvalidFilterHandler : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            try { return await next(context); }
            catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest); }
        }
    }
}
