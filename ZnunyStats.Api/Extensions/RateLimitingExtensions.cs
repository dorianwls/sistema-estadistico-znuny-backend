using System.Threading.RateLimiting;

namespace ZnunyStats.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string ReportsPolicy = "reports";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Protección base para todos los endpoints, por IP. Holgada: el dashboard hace varias consultas por pantalla.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 300,
                        QueueLimit = 0
                    }));

            // Generar un PDF o un Excel recalcula todo el período: se limita para no saturar el servidor.
            options.AddPolicy(ReportsPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    GetPartitionKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        PermitLimit = 10,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }

    private static string GetPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
