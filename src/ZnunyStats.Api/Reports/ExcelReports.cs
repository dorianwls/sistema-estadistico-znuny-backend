using ClosedXML.Excel;
using ZnunyStats.Api.Analytics;

namespace ZnunyStats.Api.Reports;

/// <summary>Reportes descargables. Cada libro incluye una hoja "Filtros" para que la cifra viaje con su contexto.</summary>
public sealed class ExcelReports(AnalyticsService analytics)
{
    public static readonly string[] Kinds = ["servicio", "atencion", "agentes"];

    public async Task<(byte[] Content, string FileName)?> BuildAsync(string kind, TicketQuery q, CancellationToken ct)
    {
        using var book = new XLWorkbook();
        Meta meta;

        switch (kind)
        {
            case "servicio":
                var overview = await analytics.OverviewAsync(q, ct);
                meta = overview.Meta;
                IndicatorsSheet(book, "Indicadores", [("Período actual", overview.Indicators), ("Período anterior", overview.PreviousIndicators)]);
                TicketsSheet(book, "Tickets", await analytics.AllTicketsAsync(q, "all", ct));
                break;
            case "atencion":
                var attention = await analytics.AllTicketsAsync(q, "attention", ct);
                meta = (await analytics.OverviewAsync(q, ct)).Meta;
                TicketsSheet(book, "Requieren atención", attention);
                break;
            case "agentes":
                var agents = await analytics.AgentsAsync(q, ct);
                meta = agents.Meta;
                AgentsSheet(book, agents);
                IndicatorsSheet(book, "Equipo", [("Equipo completo", agents.Team)]);
                break;
            default:
                return null;
        }

        FiltersSheet(book, meta, q);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return (stream.ToArray(), $"znuny-{kind}-{meta.From:yyyyMMdd}-{meta.To:yyyyMMdd}.xlsx");
    }

    private static void IndicatorsSheet(XLWorkbook book, string name, (string Label, Indicators Value)[] columns)
    {
        var ws = book.Worksheets.Add(name);
        ws.Cell(1, 1).Value = "Indicador";
        ws.Cell(1, 2).Value = "Dimensión";
        for (var c = 0; c < columns.Length; c++) ws.Cell(1, 3 + c).Value = columns[c].Label;
        ws.Cell(1, 3 + columns.Length).Value = "Cómo se calcula";

        var rows = new (string Name, string Dimension, Func<Indicators, XLCellValue> Value, string Formula)[]
        {
            ("Eficacia (%)", "Eficacia", i => Pct(i.Eficacia), "Tickets resueltos / tickets a cargo creados en el período."),
            ("Efectividad (%)", "Efectividad", i => Pct(i.Efectividad), "Resueltos dentro del objetivo y sin reapertura / tickets con resultado conocido. Excluye cierres masivos."),
            ("Tiempo de resolución, mediana (h)", "Eficiencia", i => Hours(i.ResolutionTime.MedianSeconds), "Creación → primer cierre exitoso. Excluye cierres masivos."),
            ("Tiempo de resolución, p90 (h)", "Eficiencia", i => Hours(i.ResolutionTime.P90Seconds), "9 de cada 10 tickets se resolvieron en este tiempo o menos."),
            ("Primera atención, mediana (h)", "Eficiencia", i => Hours(i.FirstAttentionTime.MedianSeconds), "Creación → primera acción de un agente."),
            ("Reapertura (%)", "Calidad", i => Pct(i.ReopenRate), "Resueltos que volvieron a abrirse dentro de la ventana / resueltos con ventana cumplida."),
            ("Tickets evaluados (efectividad)", "Muestra", i => i.Efectividad.Denominator, "Tickets cuyo resultado ya se conoce."),
            ("Tickets a cargo", "Muestra", i => i.Eficacia.Denominator, "Población del período."),
        };

        for (var r = 0; r < rows.Length; r++)
        {
            ws.Cell(r + 2, 1).Value = rows[r].Name;
            ws.Cell(r + 2, 2).Value = rows[r].Dimension;
            for (var c = 0; c < columns.Length; c++) ws.Cell(r + 2, 3 + c).Value = rows[r].Value(columns[c].Value);
            ws.Cell(r + 2, 3 + columns.Length).Value = rows[r].Formula;
        }
        Finish(ws);
    }

    private static void TicketsSheet(XLWorkbook book, string name, IReadOnlyList<TicketRow> tickets)
    {
        var ws = book.Worksheets.Add(name);
        string[] headers = ["Ticket", "Título", "Creado", "Área solicitante", "Unidad", "Categoría", "Ubicación", "Prioridad",
            "Estado", "Agente", "Primera atención (h)", "Resolución (h)", "Objetivo (h)", "Antigüedad (h)", "Atrasado", "Cierre masivo", "Reabierto"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < tickets.Count; r++)
        {
            var t = tickets[r];
            XLCellValue[] values = [t.Number, t.Title, t.CreatedAt.DateTime, t.Area, t.Unit, t.Category, t.Location, t.Priority,
                t.Status, t.Agent, Hours(t.FirstAttentionSeconds), Hours(t.ResolutionSeconds), Hours(t.TargetSeconds),
                Hours(t.AgeSeconds), YesNo(t.Overdue), YesNo(t.BulkClosed), YesNo(t.Reopened)];
            for (var c = 0; c < values.Length; c++) ws.Cell(r + 2, c + 1).Value = values[c];
        }
        ws.Column(3).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        Finish(ws);
    }

    private static void AgentsSheet(XLWorkbook book, AgentsResponse data)
    {
        var ws = book.Worksheets.Add("Agentes");
        string[] headers = ["Agente", "Unidades", "A cargo", "Resueltos", "Abiertos", "Atrasados", "Cierres masivos",
            "Eficacia (%)", "Efectividad (%)", "Evaluados efectividad", "Resolución mediana (h)", "Primera atención mediana (h)", "Reapertura (%)"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < data.Agents.Count; r++)
        {
            var a = data.Agents[r];
            XLCellValue[] values = [a.Name, string.Join(", ", a.Units), a.Assigned, a.Resolved, a.Open, a.Overdue, a.BulkClosed,
                Pct(a.Indicators.Eficacia), Pct(a.Indicators.Efectividad), a.Indicators.Efectividad.Denominator,
                Hours(a.Indicators.ResolutionTime.MedianSeconds), Hours(a.Indicators.FirstAttentionTime.MedianSeconds),
                Pct(a.Indicators.ReopenRate)];
            for (var c = 0; c < values.Length; c++) ws.Cell(r + 2, c + 1).Value = values[c];
        }
        Finish(ws);
    }

    private static void FiltersSheet(XLWorkbook book, Meta meta, TicketQuery q)
    {
        var ws = book.Worksheets.Add("Filtros");
        (string, XLCellValue)[] rows =
        [
            ("Período (creación)", $"{meta.From:dd/MM/yyyy} – {meta.To:dd/MM/yyyy}"),
            ("Período anterior", $"{meta.PreviousFrom:dd/MM/yyyy} – {meta.PreviousTo:dd/MM/yyyy}"),
            ("Zona horaria", meta.TimeZone),
            ("Datos leídos de Znuny", meta.DataLoadedAt.DateTime),
            ("Área solicitante", q.Area ?? "Todas"),
            ("Unidad", q.Unit ?? "Todas"),
            ("Categoría", q.Category ?? "Todas"),
            ("Estado", q.Status ?? "Todos"),
            ("Agente (id)", q.AgentId?.ToString() ?? "Todos"),
            ("Prioridad (id)", q.PriorityId?.ToString() ?? "Todas"),
            ("Ubicación", q.Location ?? "Todas"),
            ("Tickets en la población", meta.Population),
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            ws.Cell(r + 1, 1).Value = rows[r].Item1;
            ws.Cell(r + 1, 2).Value = rows[r].Item2;
        }
        ws.Cell(4, 2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Column(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
    }

    private static void Finish(IXLWorksheet ws)
    {
        var header = ws.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF3FF");
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns().AdjustToContents(1, 200);
    }

    private static XLCellValue Pct(RateKpi k) => k.Percent is { } p ? p : Blank.Value;
    private static XLCellValue Hours(double? seconds) => seconds is { } s ? Math.Round(s / 3600, 2) : Blank.Value;
    private static XLCellValue YesNo(bool value) => value ? "Sí" : "No";
}
