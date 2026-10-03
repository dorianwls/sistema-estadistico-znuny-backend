using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ZnunyStats.Api.Database;

/// <summary>Comprueba que la base de Znuny responde (un <c>select 1</c> en modo solo lectura).</summary>
public sealed class ZnunyHealthCheck(ZnunyQueries queries) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await queries.PingAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("La base de Znuny no respondió.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("No se pudo conectar con la base de Znuny.", ex);
        }
    }
}
