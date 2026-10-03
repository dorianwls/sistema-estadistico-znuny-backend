using Microsoft.Extensions.DependencyInjection;

using ZnunyStats.Reports.Abstractions;
using ZnunyStats.Reports.Infrastructure;

namespace ZnunyStats.Reports.Extensions;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // Licencia Community de QuestPDF: gratuita para instituciones académicas y organizaciones con ingresos < USD 1 M.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Sin estado: una instancia de cada renderizador para toda la aplicación.
        services.AddSingleton<IReportRenderer, ExcelReportRenderer>();
        services.AddSingleton<IReportRenderer, PdfReportRenderer>();

        return services;
    }
}
