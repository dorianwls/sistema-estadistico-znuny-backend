namespace ZnunyStats.Api.Common;

/// <summary>
/// Reglas de negocio configurables (sección "Estadisticas" de appsettings.json).
/// Todos los valores por defecto viven en appsettings.json para que haya una sola fuente de verdad.
/// </summary>
public sealed class StatsOptions
{
    public const string Section = "Estadisticas";

    /// <summary>Zona horaria en la que se presentan y agrupan las fechas.</summary>
    public string TimeZone { get; set; } = "America/Managua";

    /// <summary>Zona en la que Znuny guarda sus fechas (timestamp sin zona).</summary>
    public string SourceTimeZone { get; set; } = "UTC";

    /// <summary>Usuarios técnicos (root, cuentas de sistema): no cuentan como agentes.</summary>
    public int[] SystemUserIds { get; set; } = [];

    /// <summary>Estados de Znuny que significan "resuelto con éxito".</summary>
    public string[] SuccessStates { get; set; } = [];

    /// <summary>Colas que no representan demanda real (spam, correo crudo, papelera).</summary>
    public string[] ExcludedQueues { get; set; } = [];

    /// <summary>Patrones LIKE de títulos generados automáticamente (p. ej. avisos del planificador).</summary>
    public string[] ExcludedTitlePatterns { get; set; } = [];

    /// <summary>Días tras la resolución en los que volver a abrir el ticket cuenta como reapertura.</summary>
    public int ReopenWindowDays { get; set; } = 7;

    /// <summary>Minutos entre una acción masiva (Bulk) y el cierre para considerarlo "cierre masivo".</summary>
    public int BulkCloseWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Objetivo de resolución en horas por nombre de prioridad. Znuny no tiene SLA configurado,
    /// así que estos objetivos son una referencia interna, no un compromiso contractual.
    /// </summary>
    public Dictionary<string, double> ResolutionTargetHours { get; set; } = [];

    public double DefaultResolutionTargetHours { get; set; } = 24;

    /// <summary>
    /// Metas de referencia de los indicadores, usadas en los reportes para marcar el estado (cumple / cerca / bajo la meta).
    /// Igual que los objetivos por prioridad, son una referencia interna ajustable, no un compromiso contractual.
    /// </summary>
    public IndicatorGoals Goals { get; set; } = new();

    /// <summary>Ubicaciones del mapa institucional; se asignan buscando el texto en el nombre de la cola.</summary>
    public LocationOption[] Locations { get; set; } = [];

    public int CacheSeconds { get; set; } = 60;
}

public sealed class IndicatorGoals
{
    /// <summary>Porcentaje mínimo de tickets resueltos sobre recibidos.</summary>
    public double EficaciaPercent { get; set; } = 90;

    /// <summary>Porcentaje mínimo de tickets resueltos a tiempo y sin reabrir.</summary>
    public double EfectividadPercent { get; set; } = 80;

    /// <summary>Puntos por debajo de la meta que todavía se consideran "cerca".</summary>
    public double NearMarginPoints { get; set; } = 10;
}

public sealed class LocationOption
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Texto que debe contener el nombre de la cola. Vacío = ubicación por defecto.</summary>
    public string QueueMatch { get; set; } = "";

    /// <summary>Coordenadas para el mapa satelital (grados decimales).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
