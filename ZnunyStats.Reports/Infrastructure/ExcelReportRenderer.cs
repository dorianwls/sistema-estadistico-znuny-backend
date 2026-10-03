using ClosedXML.Excel;
using ZnunyStats.Domain.ReadModels;
using ZnunyStats.Reports.Abstractions;
using ZnunyStats.Reports.Content;

namespace ZnunyStats.Reports.Infrastructure;

/// <summary>
/// Libro Excel para analizar los datos. Convenciones:
/// - La primera hoja es el resumen (qué es, de cuándo, con qué filtros, cifras clave); la última explica el método.
/// - Cada hoja de datos es una tabla de Excel con encabezado fijo y filtros, una sola tabla por hoja.
/// - Porcentajes como fracción con formato % y tiempos en horas decimales: se pueden sumar y graficar sin limpiar.
/// </summary>
internal sealed class ExcelReportRenderer : IReportRenderer
{
    public string Format => "xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string PctFormat = "0.0%";
    private const string HoursFormat = "0.00";
    private const string DateFormat = "dd/mm/yyyy hh:mm";
    private static readonly XLColor Accent = XLColor.FromHtml("#0F6E8C");

    public byte[] Render(ReportContent r)
    {
        using var book = new XLWorkbook();
        SummarySheet(book, r);

        if (r.Overview is { } o)
        {
            Table(book, "Serie", ["Tramo", "Recibidos", "Resueltos", "Sin resolver", "Cerrados sin éxito", "Eficacia", "Efectividad", "Resolución mediana (h)"],
                o.Series.Select(s => new XLCellValue[] { s.Label, s.Received, s.Resolved, s.Open, s.Unsuccessful, Pct(s.Eficacia), Pct(s.Efectividad), Hours(s.MedianResolutionSeconds) }),
                pct: [6, 7], hours: [8]);
            Table(book, "Resultados", ["Resultado", "Tickets", "Se evalúa en efectividad"],
                o.Outcomes.Select(x => new XLCellValue[] { x.Label, x.Count, x.Key is "pending" or "excluded" ? "No" : "Sí" }));
            Table(book, "Por prioridad", ["Prioridad", "Objetivo (h)", "Evaluados", "A tiempo", "Cumplimiento", "Resolución mediana (h)"],
                o.ByPriority.Select(p => new XLCellValue[] { p.Label, p.TargetHours, p.Evaluated, p.OnTime, Pct(p.Percent), Hours(p.MedianSeconds) }),
                pct: [5], hours: [6]);
            GroupSheet(book, "Por equipo", "Equipo de TI", o.UnitScores);
            GroupSheet(book, "Por área", "Área solicitante", o.AreaScores);
            Table(book, "Por categoría", ["Categoría", "Resolución mediana (h)", "Tickets medidos"],
                o.ResolutionByCategory.Select(c => new XLCellValue[] { c.Label, Hours(c.MedianSeconds), c.Sample }), hours: [2]);
            Table(book, "Tiempos", ["Rango de resolución", "Tickets", "Porcentaje"],
                o.ResolutionDistribution.Select(b => new XLCellValue[] { b.Label, b.Count, b.Percent / 100 }), pct: [3]);
        }

        if (r.Agents is { } a)
            Table(book, "Agentes", ["Agente", "Equipos", "A cargo", "Resueltos", "Abiertos", "Atrasados", "Cierres masivos", "Eficacia", "Efectividad", "Evaluados efectividad", "Resolución mediana (h)", "Primera atención mediana (h)", "Reapertura"],
                a.Agents.Select(x => new XLCellValue[] { x.Name, string.Join(", ", x.Units), x.Assigned, x.Resolved, x.Open, x.Overdue, x.BulkClosed,
                    Pct(x.Indicators.Eficacia.Percent), Pct(x.Indicators.Efectividad.Percent), x.Indicators.Efectividad.Denominator,
                    Hours(x.Indicators.ResolutionTime.MedianSeconds), Hours(x.Indicators.FirstAttentionTime.MedianSeconds), Pct(x.Indicators.ReopenRate.Percent) }),
                pct: [8, 9, 13], hours: [11, 12]);

        if (r.Tickets.Count > 0 || r.Header.Kind == "atencion")
            Table(book, r.Header.Kind == "atencion" ? "Requieren atención" : "Tickets",
                ["Ticket", "Título", "Creado", "Área solicitante", "Equipo de TI", "Categoría", "Ubicación", "Prioridad", "Estado", "Agente",
                 "Primera atención (h)", "Resolución (h)", "Objetivo (h)", "Antigüedad (h)", "Atrasado", "Cierre masivo", "Reabierto"],
                r.Tickets.Select(t => new XLCellValue[] { t.Number, t.Title, t.CreatedAt.DateTime, t.Area, t.Unit, t.Category, t.Location, t.Priority,
                    t.Status, t.Agent, Hours(t.FirstAttentionSeconds), Hours(t.ResolutionSeconds), Hours(t.TargetSeconds),
                    Hours(t.AgeSeconds), YesNo(t.Overdue), YesNo(t.BulkClosed), YesNo(t.Reopened) }),
                hours: [11, 12, 13, 14], dates: [3]);

        MethodSheet(book, r);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SummarySheet(XLWorkbook book, ReportContent r)
    {
        var ws = book.Worksheets.Add("Resumen");
        ws.SetTabColor(Accent);
        var h = r.Header;
        ws.Cell(1, 1).Value = h.Title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(Accent);
        ws.Cell(2, 1).Value = h.Purpose;
        ws.Cell(2, 1).Style.Font.SetFontColor(XLColor.FromHtml("#637082"));

        var row = 4;
        foreach (var (label, value) in ReportText.Context(r))
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 2).Value = value;
            row++;
        }

        row++;
        Heading(ws, row++, "Hallazgos principales");
        foreach (var line in r.Highlights) ws.Cell(row++, 1).Value = $"• {line}";

        row++;
        Heading(ws, row++, "Indicadores");
        var kpiTop = row;
        string[] headers = ["Indicador", "Dimensión", "Período actual", "Período anterior", "Variación", "Meta", "Estado", "Cómo se calcula"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(row, c + 1).Value = headers[c];
        foreach (var k in r.Kpis)
        {
            row++;
            ws.Cell(row, 1).Value = k.Name;
            ws.Cell(row, 2).Value = k.Dimension;
            KpiValue(ws.Cell(row, 3), k.Unit, k.Current);
            KpiValue(ws.Cell(row, 4), k.Unit, k.Previous);
            ws.Cell(row, 5).Value = ReportText.VariationText(k);
            ws.Cell(row, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            KpiValue(ws.Cell(row, 6), k.Unit, k.Goal);
            ws.Cell(row, 7).Value = ReportText.StatusText(k.Status);
            if (ReportText.StatusColor(k.Status) is { } color) ws.Cell(row, 7).Style.Fill.SetBackgroundColor(XLColor.FromHtml(color.Soft)).Font.SetFontColor(XLColor.FromHtml(color.Ink));
            ws.Cell(row, 8).Value = k.Formula;
        }
        var table = ws.Range(kpiTop, 1, row, headers.Length).CreateTable("Indicadores");
        table.Theme = XLTableTheme.TableStyleLight9;

        if (r.Notices.Count > 0)
        {
            row += 2;
            Heading(ws, row++, "Avisos sobre los datos");
            foreach (var n in r.Notices) ws.Cell(row++, 1).Value = $"• {n.Message}";
        }

        ws.Column(1).Width = 34;
        ws.Columns(2, 7).AdjustToContents(kpiTop, row, 10, 22);
        ws.Column(8).Width = 90;
        ws.Column(8).Style.Alignment.SetWrapText();
        ws.SheetView.ZoomScale = 110;
    }

    private static void MethodSheet(XLWorkbook book, ReportContent r)
    {
        var ws = book.Worksheets.Add("Método");
        var row = 1;
        Heading(ws, row++, "Filtros aplicados");
        foreach (var (label, value) in r.Header.Filters) Pair(ws, row++, label, value);
        row++;

        Heading(ws, row++, "Cómo se calcula cada indicador");
        foreach (var (name, formula) in ReportText.Definitions) Pair(ws, row++, name, formula);
        row++;

        Heading(ws, row++, "Objetivos de resolución por prioridad");
        foreach (var t in r.Targets) Pair(ws, row++, t.Priority, $"{t.Hours} horas");
        Pair(ws, row++, "Nota", "Znuny no tiene SLA configurado: los objetivos y metas son referencias internas ajustables en la configuración del backend.");

        if (r.Quality is { } q)
        {
            row++;
            Heading(ws, row++, "Calidad de los datos");
            foreach (var c in q.Checks)
                Pair(ws, row++, c.Title, (c.Affected is { } n ? $"{n} de {c.OutOf}. " : "") + c.Detail);
        }

        ws.Column(1).Width = 36;
        ws.Column(2).Width = 110;
        ws.Column(2).Style.Alignment.SetWrapText();
    }

    private static void GroupSheet(XLWorkbook book, string name, string label, IReadOnlyList<GroupScore> rows) =>
        Table(book, name, [label, "Tickets", "Eficacia", "Efectividad", "Resolución mediana (h)"],
            rows.Select(g => new XLCellValue[] { g.Label, g.Received, Pct(g.Eficacia), Pct(g.Efectividad), Hours(g.MedianResolutionSeconds) }),
            pct: [3, 4], hours: [5]);

    /// <summary>Una tabla de Excel por hoja: encabezado fijo, filtros, formatos por columna (índices desde 1).</summary>
    private static void Table(XLWorkbook book, string name, string[] headers, IEnumerable<XLCellValue[]> rows, int[]? pct = null, int[]? hours = null, int[]? dates = null)
    {
        var ws = book.Worksheets.Add(name);
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        var r = 1;
        foreach (var values in rows)
        {
            r++;
            for (var c = 0; c < values.Length; c++) ws.Cell(r, c + 1).Value = values[c];
        }

        // Una tabla de Excel necesita al menos una fila de datos.
        if (r == 1) ws.Cell(2, 1).Value = "Sin datos para los filtros aplicados";
        var table = ws.Range(1, 1, Math.Max(r, 2), headers.Length).CreateTable(TableName(name));
        table.Theme = XLTableTheme.TableStyleLight9;

        foreach (var c in pct ?? []) ws.Column(c).Style.NumberFormat.Format = PctFormat;
        foreach (var c in hours ?? []) ws.Column(c).Style.NumberFormat.Format = HoursFormat;
        foreach (var c in dates ?? []) ws.Column(c).Style.DateFormat.Format = DateFormat;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(r, 500), 8, 60);
    }

    private static void KpiValue(IXLCell cell, KpiUnit unit, double? value)
    {
        if (value is not { } v) { cell.Value = "—"; cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right); return; }
        switch (unit)
        {
            case KpiUnit.Percent: cell.Value = v / 100; cell.Style.NumberFormat.Format = PctFormat; break;
            case KpiUnit.Duration: cell.Value = ReportText.Duration(v); cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right); break;
            default: cell.Value = v; cell.Style.NumberFormat.Format = "#,##0.##"; break;
        }
    }

    private static void Heading(IXLWorksheet ws, int row, string text)
    {
        ws.Cell(row, 1).Value = text;
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(Accent);
    }

    private static void Pair(IXLWorksheet ws, int row, string label, string value)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Font.SetBold().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        ws.Cell(row, 2).Value = value;
    }

    private static string TableName(string sheet) => "T_" + new string(sheet.Where(char.IsLetterOrDigit).ToArray());

    private static XLCellValue Pct(double? percent) => percent is { } p ? p / 100 : Blank.Value;
    private static XLCellValue Hours(double? seconds) => seconds is { } s ? Math.Round(s / 3600, 2) : Blank.Value;
    private static XLCellValue YesNo(bool value) => value ? "Sí" : "No";
}
