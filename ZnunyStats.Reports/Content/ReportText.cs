namespace ZnunyStats.Reports.Content;

/// <summary>Textos y formatos compartidos por el PDF y el Excel: así una cifra se lee igual en ambos.</summary>
public static class ReportText
{
    public static readonly (string Name, string Formula)[] Definitions =
    [
        ("Eficacia", "Tickets resueltos ÷ tickets recibidos en el período. ¿Se resolvió lo que se recibió?"),
        ("Efectividad", "Resueltos dentro del objetivo de su prioridad y sin reabrirse ÷ tickets con resultado conocido. Excluye cierres masivos y abiertos aún en plazo."),
        ("Eficiencia", "Mediana del tiempo entre la creación y el primer cierre exitoso: la mitad de los tickets tardó menos. Excluye cierres masivos."),
        ("9 de cada 10", "Percentil 90 del tiempo de resolución: muestra cuánto tardan los casos lentos."),
        ("Primera atención", "Mediana del tiempo hasta la primera acción de un agente. Excluye tickets abiertos por el propio agente."),
        ("Reapertura", "Resueltos que volvieron a abrirse dentro de la ventana ÷ resueltos con la ventana ya cumplida."),
    ];

    public static string Pct(double? v) => v is { } p ? $"{p:0.#} %" : "—";

    /// <summary>Misma presentación que el dashboard: "18 m", "3 h 05 m", "2 d 4 h".</summary>
    public static string Duration(double? seconds)
    {
        if (seconds is not { } s) return "—";
        var minutes = (int)Math.Round(s / 60);
        if (minutes < 60) return $"{minutes} m";
        var hours = minutes / 60;
        if (hours < 48) return $"{hours} h {minutes % 60:00} m";
        return $"{hours / 24} d {hours % 24} h";
    }

    public static IEnumerable<(string Label, string Value)> Context(ReportContent r)
    {
        var m = r.Header.Meta;
        yield return ("Período (fecha de creación)", $"{m.From:dd/MM/yyyy} – {m.To:dd/MM/yyyy}");
        yield return ("Período anterior (comparación)", $"{m.PreviousFrom:dd/MM/yyyy} – {m.PreviousTo:dd/MM/yyyy}");
        yield return ("Tickets en la población", m.Population.ToString());
        foreach (var f in r.Header.Filters.Where(f => !f.Value.StartsWith("Tod"))) yield return (f.Label, f.Value);
        yield return ("Datos leídos de Znuny", m.DataLoadedAt.ToString("dd/MM/yyyy HH:mm"));
        yield return ("Generado", r.Header.GeneratedAt.ToString("dd/MM/yyyy HH:mm"));
        yield return ("Zona horaria", m.TimeZone);
    }

    /// <summary>Tasas en puntos porcentuales ("+6 pts"), tiempos como duración con signo, conteos como diferencia.</summary>
    public static string VariationText(KpiLine k)
    {
        if (k.Variation is not { } v) return "—";
        var sign = v > 0 ? "+" : v < 0 ? "−" : "";
        return k.Unit switch
        {
            KpiUnit.Percent => $"{sign}{Math.Abs(v):0.#} pts",
            KpiUnit.Duration => $"{sign}{Duration(Math.Abs(v))}",
            _ => $"{sign}{Math.Abs(v):#,##0}",
        };
    }

    public static string StatusText(KpiStatus s) => s switch
    {
        KpiStatus.Meets => "Cumple la meta",
        KpiStatus.Near => "Cerca de la meta",
        KpiStatus.Below => "Bajo la meta",
        KpiStatus.Better => "Mejoró",
        KpiStatus.Worse => "Empeoró",
        KpiStatus.Same => "Sin cambio",
        KpiStatus.NoData => "Sin datos",
        _ => "",
    };

    /// <summary>Colores de estado (texto, fondo suave): los mismos del dashboard.</summary>
    public static (string Ink, string Soft)? StatusColor(KpiStatus s) => s switch
    {
        KpiStatus.Meets or KpiStatus.Better => ("#2E9467", "#E1F2E9"),
        KpiStatus.Near => ("#9A6408", "#FBEFD9"),
        KpiStatus.Below or KpiStatus.Worse => ("#C9483F", "#FBE7E5"),
        _ => null,
    };
}
