using ZnunyStats.Api.Extensions;
using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Reports.Abstractions;

namespace ZnunyStats.Api.Modules.Reports.ExportReport;

public static class ExportReportEndpoint
{
    public static void MapExportReport(this RouteGroupBuilder group)
    {
        group.MapGet("/{kind}", Handle)
            .RequireRateLimiting(RateLimitingExtensions.ReportsPolicy)
            .WithSummary("Exporta un reporte (servicio, atencion o agentes) en Excel (format=xlsx, para analizar) o PDF (format=pdf, para compartir).");
    }

    private static async Task<IResult> Handle(
        string kind,
        [AsParameters] TicketQuery query,
        ReportContentBuilder builder,
        IEnumerable<IReportRenderer> renderers,
        CancellationToken cancellationToken,
        string format = "xlsx")
    {
        var renderer = renderers.FirstOrDefault(r => r.Format == format);
        if (renderer is null)
        {
            return TypedResults.Problem(
                $"Formato desconocido. Opciones: {string.Join(", ", renderers.Select(r => r.Format))}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (await builder.BuildAsync(kind, query, cancellationToken) is not { } content)
        {
            return TypedResults.Problem(
                $"Reporte desconocido. Opciones: {string.Join(", ", ReportContentBuilder.Kinds)}",
                statusCode: StatusCodes.Status404NotFound);
        }

        var meta = content.Header.Meta;
        var fileName = $"znuny-{kind}-{meta.From:yyyyMMdd}-{meta.To:yyyyMMdd}.{renderer.Format}";
        return TypedResults.File(renderer.Render(content), renderer.ContentType, fileName);
    }
}
