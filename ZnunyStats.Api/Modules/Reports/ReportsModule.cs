using ZnunyStats.Api.Modules.Reports.ExportReport;

namespace ZnunyStats.Api.Modules.Reports;

public static class ReportsModule
{
    public static void MapReportsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports").WithTags("Reports");

        group.MapExportReport();
    }
}
