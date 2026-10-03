using Serilog;
using Serilog.Events;

using ZnunyStats.Api.Database;
using ZnunyStats.Api.Extensions;

using ZnunyStats.Reports.Extensions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    string[] allowedOrigins = builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddAnalytics(builder.Configuration);
    builder.Services.AddReporting();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Solo lectura: GET para consultar y POST /refresh para descartar la caché.
    // Content-Disposition expuesto para que el navegador lea el nombre de los reportes descargados.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowOrigins", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .WithMethods("GET", "POST")
            .WithExposedHeaders("Content-Disposition"));
    });

    builder.Services.AddGlobalExceptionHandling();

    builder.Services.AddHealthChecks()
        .AddCheck<ZnunyHealthCheck>("znuny");

    builder.Services.AddApiRateLimiting();

    var app = builder.Build();

    app.UseGlobalExceptionHandling();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerWithUi();
    }

    app.UseCors("AllowOrigins");
    app.UseApiRateLimiting();

    // Map application endpoints
    app.MapEndpoints();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
