using Npgsql;
using ZnunyStats.Api;
using ZnunyStats.Api.Analytics;
using ZnunyStats.Api.Data;
using ZnunyStats.Api.Endpoints;
using ZnunyStats.Api.Reports;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Znuny");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Falta ConnectionStrings:Znuny (ver appsettings.Development.json o variable ConnectionStrings__Znuny).");

builder.Services.Configure<StatsOptions>(builder.Configuration.GetSection(StatsOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<ZnunyQueries>();
builder.Services.AddSingleton<TicketStore>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddSingleton<ExcelReports>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration.GetSection($"{StatsOptions.Section}:CorsOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST")
    .WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapApi();

app.Run();
