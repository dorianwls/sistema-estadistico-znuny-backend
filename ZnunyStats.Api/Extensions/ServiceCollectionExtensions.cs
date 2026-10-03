using FluentValidation;

using Npgsql;

using ZnunyStats.Api.Common;
using ZnunyStats.Api.Database;
using ZnunyStats.Api.Modules.Reports.ExportReport;
using ZnunyStats.Api.Modules.Shared;

namespace ZnunyStats.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connString = configuration.GetConnectionString("Znuny");
        if (string.IsNullOrWhiteSpace(connString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Znuny (ver appsettings.Development.json o variable ConnectionStrings__Znuny).");
        }

        // Mantener "Options=-c default_transaction_read_only=on": PostgreSQL rechaza cualquier escritura de la sesión.
        services.AddSingleton(NpgsqlDataSource.Create(connString));
        services.AddSingleton<ZnunyQueries>();

        return services;
    }

    public static IServiceCollection AddAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StatsOptions>(configuration.GetSection(StatsOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ZnunyClock>();
        services.AddMemoryCache();

        services.AddSingleton<TicketStore>();
        services.AddSingleton<TicketAnalytics>();
        services.AddSingleton<ReportContentBuilder>();

        // Validators
        services.AddSingleton<IValidator<TicketQuery>, TicketQueryValidator>();

        return services;
    }
}
