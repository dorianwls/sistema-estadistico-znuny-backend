using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.DataQuality.GetDataQuality;

/// <summary>Limitaciones de los datos del período. Lo usan el endpoint y el reporte de servicio.</summary>
public static class GetDataQualityHandler
{
    private const string NoArea = "Sin área registrada";

    public static async Task<DataQualityResponse> ExecuteAsync(TicketQuery q, TicketAnalytics a, CancellationToken ct)
    {
        var snap = await a.GetSnapshotAsync(ct);
        var period = a.ResolvePeriod(q);
        var current = a.Population(snap, q, period);
        var resolved = current.Where(t => t.ResolvedAt is not null).ToList();
        var open = current.Where(t => t.IsOpen).ToList();

        var checks = new List<QualityCheck>
        {
            new("BULK_CLOSED", Severity(resolved.Count(t => t.BulkClosed), resolved.Count),
                "Cierres masivos",
                "Tickets cerrados con la acción masiva de Znuny. Cuentan para eficacia, pero se excluyen de tiempos y efectividad porque su duración no refleja el trabajo real.",
                resolved.Count(t => t.BulkClosed), resolved.Count, ["eficiencia", "efectividad"]),
            new("OPEN_UNASSIGNED", Severity(open.Count(t => t.AgentId is null), open.Count),
                "Abiertos sin agente",
                "Tickets abiertos cuyo propietario es una cuenta de sistema. No se pueden atribuir a ningún agente.",
                open.Count(t => t.AgentId is null), open.Count, ["agentes"]),
            new("NO_AREA", Severity(current.Count(t => t.Area == NoArea), current.Count),
                "Solicitante sin área",
                "El cliente del ticket no tiene una empresa/área asociada en Znuny.",
                current.Count(t => t.Area == NoArea), current.Count, ["área solicitante"]),
            new("RESOLVED_WITHOUT_ATTENTION", Severity(resolved.Count(t => t.FirstAttentionAt is null), resolved.Count),
                "Resueltos sin acción de agente",
                "Tickets resueltos sin ninguna acción registrada por una persona (cerrados por el sistema).",
                resolved.Count(t => t.FirstAttentionAt is null), resolved.Count, ["primera atención"]),
            snap.Capabilities.ActiveSlas == 0
                ? new("NO_SLA", "warning", "Sin SLA configurado",
                    "Znuny no tiene SLA activos. La efectividad usa objetivos de referencia por prioridad definidos en la configuración del backend.",
                    null, null, ["efectividad"])
                : new("SLA_AVAILABLE", "ok", "SLA configurado", "Existen SLA activos en Znuny.", null, null, []),
            snap.Capabilities.TimeAccountingEntries == 0
                ? new("NO_TIME_ACCOUNTING", "info", "Sin registro de tiempo",
                    "Los agentes no registran tiempo trabajado (time_accounting vacío). No se puede medir esfuerzo, solo tiempo transcurrido.",
                    null, null, ["eficiencia"])
                : new("TIME_ACCOUNTING", "ok", "Registro de tiempo disponible", "Existen registros de tiempo trabajado.", null, null, []),
        };
        return new DataQualityResponse(a.Meta(snap, period, current.Count), checks);
    }

    private static string Severity(int affected, int outOf) =>
        affected == 0 ? "ok" : outOf > 0 && affected * 100 / outOf >= 25 ? "warning" : "info";
}
