using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZnunyStats.Domain.ReadModels;
using ZnunyStats.Reports.Abstractions;
using ZnunyStats.Reports.Content;

namespace ZnunyStats.Reports.Infrastructure;

/// <summary>
/// Informe PDF para leer y compartir. Estructura de pirámide invertida:
/// 1) qué es y con qué filtros, 2) resumen ejecutivo (indicadores con meta y estado, hallazgos),
/// 3) gráficos y desgloses, 4) método y calidad de los datos. Cada página repite título, período y paginación.
/// </summary>
internal sealed class PdfReportRenderer : IReportRenderer
{
    public string Format => "pdf";
    public string ContentType => "application/pdf";

    // Paleta del dashboard (modo claro). Las series usan los mismos colores que los gráficos web.
    private const string Ink = "#1A2230", Ink2 = "#3C4859", Muted = "#637082", Faint = "#939DAB";
    private const string Line = "#DFE4E3", Sunken = "#F5F7F6", Accent = "#0F6E8C", AccentSoft = "#E2EFF3";
    private const string Eficacia = "#2A6FDB", Efectividad = "#2E9467", Eficiencia = "#7B5CD6", Open = "#D08A1A", Fail = "#C23A6A";

    private static readonly Dictionary<string, string> OutcomeColor = new()
    {
        ["onTime"] = Efectividad, ["late"] = "#D08A1A", ["reopened"] = "#7B5CD6", ["overdue"] = "#D4483E", ["unsuccessful"] = "#3F6FAE",
    };

    /// <summary>Más filas que esto no se leen en papel: el detalle completo va en el Excel.</summary>
    private const int MaxTicketRows = 300;

    public byte[] Render(ReportContent r) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(r.Header.Kind == "atencion" ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(32);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink).FontFamily(Fonts.Lato).LineHeight(1.3f));
            page.Header().Element(c => Header(c, r.Header));
            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(16);
                col.Item().Element(c => Cover(c, r));
                col.Item().Element(c => Summary(c, r));
                switch (r.Header.Kind)
                {
                    case "servicio": ServiceBody(col, r.Overview!); break;
                    case "atencion": AttentionBody(col, r.Tickets); break;
                    case "agentes": AgentsBody(col, r.Agents!); break;
                }
                col.Item().Element(c => Method(c, r));
            });
            page.Footer().Element(c => Footer(c, r.Header));
        }))
        .WithMetadata(new DocumentMetadata
        {
            Title = r.Header.Title,
            Subject = $"{r.Header.Purpose} Período {r.Header.Meta.From:dd/MM/yyyy} – {r.Header.Meta.To:dd/MM/yyyy}.",
            Author = "IT Service Desk · Znuny",
            Creator = "ZnunyStats",
            CreationDate = r.Header.GeneratedAt,
        })
        .GeneratePdf();

    // ---------- Marco de página ----------

    private static void Header(IContainer c, ReportHeader h) =>
        c.BorderBottom(1.5f).BorderColor(Accent).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("IT Service Desk · ").FontColor(Muted).FontSize(8);
                t.Span(h.Title).SemiBold().FontSize(8);
            });
            row.AutoItem().Text($"{h.Meta.From:dd/MM/yyyy} – {h.Meta.To:dd/MM/yyyy}").FontSize(8).FontColor(Muted);
        });

    private static void Footer(IContainer c, ReportHeader h) =>
        c.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Generado el {h.GeneratedAt:dd/MM/yyyy HH:mm} · datos de Znuny al {h.Meta.DataLoadedAt:dd/MM/yyyy HH:mm} ({h.Meta.TimeZone})").FontSize(7.5f).FontColor(Faint);
            row.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });

    private static void Cover(IContainer c, ReportContent r) => c.Column(col =>
    {
        var h = r.Header;
        col.Item().Text(h.Title).FontSize(20).Bold();
        col.Item().PaddingTop(2).Text(h.Purpose).FontSize(10).FontColor(Muted);
        col.Item().PaddingTop(10).Background(Sunken).CornerRadius(6).Padding(10).Column(box =>
        {
            box.Spacing(3);
            foreach (var (label, value) in ReportText.Context(r).Where(x => !x.Label.StartsWith("Generado") && !x.Label.StartsWith("Datos leídos") && x.Label != "Zona horaria"))
                box.Item().Text(t => { t.Span($"{label}: ").FontColor(Muted); t.Span(value).SemiBold(); });
        });
    });

    // ---------- Resumen ejecutivo ----------

    private static void Summary(IContainer c, ReportContent r) => c.Column(col =>
    {
        col.Spacing(10);
        col.Item().Element(x => SectionTitle(x, "Resumen", "Lo esencial en una página"));

        var tiles = r.Header.Kind == "atencion" ? r.Kpis.ToList() : r.Kpis.Where(k => k.Name is "Eficacia" or "Efectividad" or "Tiempo de resolución (mediana)").ToList();
        col.Item().Row(row =>
        {
            row.Spacing(8);
            foreach (var k in tiles) row.RelativeItem().Element(x => KpiTile(x, k));
        });

        if (r.Highlights.Count > 0)
            col.Item().Background(AccentSoft).CornerRadius(6).Padding(10).Column(box =>
            {
                box.Spacing(4);
                box.Item().Text("Hallazgos principales").SemiBold().FontColor(Accent);
                foreach (var line in r.Highlights)
                    box.Item().Row(row => { row.ConstantItem(10).Text("•").FontColor(Accent); row.RelativeItem().Text(line).FontColor(Ink2); });
            });

        foreach (var n in r.Notices.Where(n => n.Severity == "warning"))
            col.Item().Background("#FBEFD9").CornerRadius(6).Padding(8).Text(n.Message).FontColor("#7A5308");
    });

    private static void KpiTile(IContainer c, KpiLine k)
    {
        var (name, color) = k.Name switch
        {
            "Eficacia" => ("Eficacia", Eficacia),
            "Efectividad" => ("Efectividad", Efectividad),
            "Tiempo de resolución (mediana)" => ("Eficiencia", Eficiencia),
            _ => (k.Name, Accent),
        };
        var question = name switch
        {
            "Eficacia" => "¿Se resolvió lo que se recibió?",
            "Efectividad" => "¿Se resolvió bien y a tiempo?",
            "Eficiencia" => "Tiempo típico de resolución",
            _ => k.Formula,
        };

        c.Border(0.75f).BorderColor(Line).CornerRadius(6).Padding(10).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.AutoItem().PaddingTop(3).Width(7).Height(7).Background(color).CornerRadius(3.5f);
                row.RelativeItem().PaddingLeft(5).Text(name).SemiBold().FontSize(10);
                if (ReportText.StatusColor(k.Status) is { } s)
                    row.AutoItem().Background(s.Soft).CornerRadius(8).PaddingHorizontal(6).PaddingVertical(1).Text(ReportText.StatusText(k.Status)).FontSize(7.5f).SemiBold().FontColor(s.Ink);
            });
            col.Item().Text(question).FontSize(8).FontColor(Muted);
            col.Item().PaddingTop(6).Text(FormatKpi(k.Unit, k.Current)).FontSize(22).Bold();
            if (k.Unit == KpiUnit.Percent && k.Current is { } v)
                col.Item().PaddingTop(4).Element(x => Meter(x, v, color, k.Goal));
            if (k.Goal is not null || k.Status != KpiStatus.None || k.Previous is not null)
            col.Item().PaddingTop(6).Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                if (k.Goal is { } g) t.Span($"Meta {FormatKpi(k.Unit, g)} · ");
                t.Span(k.Previous is null ? "sin datos del período anterior" : $"antes {FormatKpi(k.Unit, k.Previous)} ({ReportText.VariationText(k)})");
            });
        });
    }

    /// <summary>Barra 0–100 con una marca vertical en la meta.</summary>
    private static void Meter(IContainer c, double percent, string color, double? goal) =>
        c.Height(6).Layers(l =>
        {
            l.PrimaryLayer().Background(Sunken).CornerRadius(3);
            l.Layer().Row(row =>
            {
                if (percent > 0) row.RelativeItem((float)percent).Background(color).CornerRadius(3);
                if (percent < 100) row.RelativeItem((float)(100 - percent));
            });
            if (goal is { } g and > 0 and < 100)
                l.Layer().Row(row => { row.RelativeItem((float)g); row.ConstantItem(1.5f).Background(Ink); row.RelativeItem((float)(100 - g)); });
        });

    // ---------- Servicio ----------

    private static void ServiceBody(ColumnDescriptor col, OverviewResponse o)
    {
        col.Item().PreventPageBreak().Column(s =>
        {
            s.Spacing(8);
            s.Item().Element(x => SectionTitle(x, "Tickets recibidos y cómo terminaron", $"{o.Volume.Received} recibidos, {o.Volume.Resolved} resueltos · por {(o.TrendBucket == "day" ? "día" : "semana")} de creación"));
            s.Item().Element(x => Legend(x, [("Resueltos", Eficacia), ("Sin resolver", Open), ("Cerrados sin éxito", Fail)]));
            s.Item().Element(x => VolumeColumns(x, o.Series));
        });

        col.Item().PreventPageBreak().Column(s =>
        {
            s.Spacing(8);
            s.Item().Element(x => SectionTitle(x, "Efectividad", "¿Se resolvió bien y a tiempo?", Efectividad));
            s.Item().Row(row =>
            {
                row.Spacing(16);
                row.RelativeItem().Element(x => Outcomes(x, o.Outcomes));
                row.RelativeItem().Element(x => Priorities(x, o.ByPriority));
            });
        });

        col.Item().PreventPageBreak().Column(s =>
        {
            s.Spacing(8);
            s.Item().Element(x => SectionTitle(x, "Eficiencia", "¿Cuánto tiempo tomó?", Eficiencia));
            s.Item().Row(row =>
            {
                row.Spacing(16);
                row.RelativeItem().Column(h =>
                {
                    h.Item().Text("Tickets según lo que tardaron en resolverse").SemiBold();
                    var peak = Math.Max(1, o.ResolutionDistribution.Select(b => b.Count).DefaultIfEmpty().Max());
                    foreach (var b in o.ResolutionDistribution)
                        h.Item().PaddingTop(5).Element(x => HBar(x, b.Label, b.Count, peak, Eficiencia, $"{b.Count} ({b.Percent:0.#} %)"));
                });
                row.RelativeItem().Column(h =>
                {
                    h.Item().Text("Tiempo típico por categoría (mediana)").SemiBold();
                    var peak = Math.Max(1, o.ResolutionByCategory.Select(c => c.MedianSeconds ?? 0).DefaultIfEmpty().Max());
                    foreach (var c in o.ResolutionByCategory)
                        h.Item().PaddingTop(5).Element(x => HBar(x, c.Label, c.MedianSeconds ?? 0, peak, Eficiencia, $"{ReportText.Duration(c.MedianSeconds)} · {c.Sample}"));
                    if (o.ResolutionByCategory.Count == 0) h.Item().PaddingTop(5).Text("No hay resoluciones medibles.").FontColor(Muted);
                });
            });
        });

        col.Item().Column(s =>
        {
            s.Spacing(8);
            s.Item().Element(x => SectionTitle(x, "Comparativo", "Los tres indicadores por equipo y por área"));
            s.Item().Element(x => GroupTable(x, "Equipo de TI", o.UnitScores));
            s.Item().PaddingTop(6).Element(x => GroupTable(x, "Área solicitante", o.AreaScores));
            s.Item().Text("Los grupos con menos de 5 tickets tienen porcentajes poco estables: un solo caso los cambia mucho.").FontSize(8).FontColor(Muted);
        });
    }

    private static void VolumeColumns(IContainer c, IReadOnlyList<SeriesPoint> series)
    {
        const float plot = 120;
        var peak = Math.Max(1, series.Select(s => s.Received).DefaultIfEmpty().Max());
        var every = Math.Max(1, (int)Math.Ceiling(series.Count / 8.0));

        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(28).Height(plot).Column(axis =>
                {
                    axis.Item().Text(peak.ToString()).FontSize(7).FontColor(Faint);
                    axis.Item().ExtendVertical().AlignBottom().Text("0").FontSize(7).FontColor(Faint);
                });
                row.RelativeItem().Height(plot).BorderBottom(0.75f).BorderColor(Line).Row(bars =>
                {
                    bars.Spacing(series.Count > 40 ? 1 : 2);
                    foreach (var s in series)
                        bars.RelativeItem().AlignBottom().Column(stack =>
                        {
                            foreach (var (value, color) in new[] { (s.Open, Open), (s.Unsuccessful, Fail), (s.Resolved, Eficacia) })
                                if (value > 0) stack.Item().Height(Math.Max(1, plot * value / peak)).Background(color);
                        });
                });
            });
            // Cada etiqueta ocupa el ancho de los tramos que resume, para que la fecha no se parta en dos líneas.
            col.Item().PaddingLeft(28).PaddingTop(3).Row(labels =>
            {
                for (var i = 0; i < series.Count; i += every)
                    labels.RelativeItem(Math.Min(every, series.Count - i)).Text(series[i].Label).FontSize(6.5f).FontColor(Faint);
            });
            var busiest = series.MaxBy(s => s.Received);
            if (busiest is { Received: > 0 })
                col.Item().PaddingTop(4).Text($"Pico: {busiest.Label} con {busiest.Received} tickets ({busiest.Resolved} resueltos).").FontSize(8).FontColor(Muted);
        });
    }

    private static void Outcomes(IContainer c, IReadOnlyList<OutcomeItem> outcomes)
    {
        var evaluated = outcomes.Where(o => OutcomeColor.ContainsKey(o.Key)).ToList();
        var total = evaluated.Sum(o => o.Count);
        c.Column(col =>
        {
            col.Item().Text("Resultado de los tickets evaluados").SemiBold();
            if (total == 0) { col.Item().PaddingTop(5).Text("Todavía no hay tickets con resultado conocido.").FontColor(Muted); return; }

            col.Item().PaddingTop(6).Height(10).Row(row =>
            {
                row.Spacing(1.5f);
                foreach (var o in evaluated.Where(o => o.Count > 0)) row.RelativeItem(o.Count).Background(OutcomeColor[o.Key]);
            });
            foreach (var o in evaluated)
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.ConstantItem(12).PaddingTop(2.5f).Width(7).Height(7).Background(OutcomeColor[o.Key]);
                    row.RelativeItem().Text(o.Label).FontColor(o.Count > 0 ? Ink2 : Faint);
                    row.ConstantItem(34).AlignRight().Text(o.Count.ToString()).SemiBold();
                    row.ConstantItem(40).AlignRight().Text($"{100.0 * o.Count / total:0.#} %").FontColor(Muted);
                });
            var pending = outcomes.FirstOrDefault(o => o.Key == "pending")?.Count ?? 0;
            var excluded = outcomes.FirstOrDefault(o => o.Key == "excluded")?.Count ?? 0;
            col.Item().PaddingTop(6).Text($"No se evalúan: {pending} abiertos aún en plazo y {excluded} cierres masivos.").FontSize(8).FontColor(Muted);
        });
    }

    private static void Priorities(IContainer c, IReadOnlyList<PriorityCompliance> items) => c.Column(col =>
    {
        col.Item().Text("Cumplimiento del objetivo por prioridad").SemiBold();
        foreach (var p in items)
            col.Item().PaddingTop(5).Element(x => HBar(x, $"{p.Label} ({p.TargetHours:0.#} h)", p.Percent ?? 0, 100, Efectividad,
                p.Evaluated == 0 ? "sin casos" : $"{ReportText.Pct(p.Percent)} · {p.OnTime}/{p.Evaluated}"));
        if (items.Count == 0) col.Item().PaddingTop(5).Text("Sin tickets en la selección.").FontColor(Muted);
    });

    private static void GroupTable(IContainer c, string label, IReadOnlyList<GroupScore> rows) =>
        c.Table(t =>
        {
            t.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(1); cols.RelativeColumn(1.4f); cols.RelativeColumn(1.4f); cols.RelativeColumn(1.4f); });
            t.Header(h =>
            {
                foreach (var title in new[] { label, "Tickets", "Eficacia", "Tiempo típico", "Efectividad" })
                    h.Cell().Element(HeaderCell).Text(title);
            });
            foreach (var g in rows)
            {
                var dim = g.Received < 5 ? Faint : Ink;
                t.Cell().Element(BodyCell).Text(g.Label).FontColor(dim);
                t.Cell().Element(BodyCell).AlignRight().Text(g.Received.ToString()).FontColor(dim);
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Pct(g.Eficacia)).FontColor(dim);
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Duration(g.MedianResolutionSeconds)).FontColor(dim);
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Pct(g.Efectividad)).FontColor(dim);
            }
        });

    // ---------- Atención ----------

    private static void AttentionBody(ColumnDescriptor col, IReadOnlyList<TicketRow> tickets)
    {
        if (tickets.Count > 0)
            col.Item().PreventPageBreak().Column(s =>
            {
                s.Spacing(8);
                s.Item().Element(x => SectionTitle(x, "Dónde se acumulan", "Para repartir el trabajo: por agente y por categoría"));
                s.Item().Row(row =>
                {
                    row.Spacing(16);
                    row.RelativeItem().Element(x => Pile(x, "Agente", tickets.GroupBy(t => t.Agent)));
                    row.RelativeItem().Element(x => Pile(x, "Categoría", tickets.GroupBy(t => t.Category)));
                });
            });
        TicketList(col, tickets);
    }

    /// <summary>Abiertos y atrasados por grupo, con barra de carga. Muestra los 8 mayores.</summary>
    private static void Pile(IContainer c, string label, IEnumerable<IGrouping<string, TicketRow>> groups)
    {
        var rows = groups.Select(g => (Label: g.Key, Open: g.Count(), Overdue: g.Count(t => t.Overdue))).OrderByDescending(x => x.Open).Take(8).ToList();
        var peak = Math.Max(1, rows.Select(x => x.Open).DefaultIfEmpty().Max());
        c.Table(t =>
        {
            t.ColumnsDefinition(cols => { cols.RelativeColumn(2.2f); cols.RelativeColumn(1.6f); cols.ConstantColumn(48); });
            t.Header(h => { h.Cell().Element(HeaderCell).Text(label); h.Cell().Element(HeaderCell).Text("Abiertos"); h.Cell().Element(HeaderCell).AlignRight().Text("Atrasados"); });
            foreach (var x in rows)
            {
                t.Cell().Element(BodyCell).Text(x.Label).FontColor(x.Label == "Sin asignar" ? "#C9483F" : Ink).ClampLines(1);
                t.Cell().Element(BodyCell).Element(cell => HBar(cell, null, x.Open, peak, Open, x.Open.ToString()));
                t.Cell().Element(BodyCell).AlignRight().Text(x.Overdue.ToString()).FontColor(x.Overdue > 0 ? "#C9483F" : Ink);
            }
        });
    }

    private static void TicketList(ColumnDescriptor col, IReadOnlyList<TicketRow> tickets) => col.Item().Column(s =>
    {
        s.Spacing(8);
        s.Item().Element(x => SectionTitle(x, "Tickets abiertos", tickets.Count > MaxTicketRows
            ? $"Los {MaxTicketRows} más urgentes de {tickets.Count}; el listado completo está en el Excel"
            : "Atrasados primero, luego por antigüedad"));
        if (tickets.Count == 0) { s.Item().Text("No hay tickets abiertos con los filtros aplicados.").FontColor(Muted); return; }

        s.Item().Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(92); cols.RelativeColumn(3); cols.ConstantColumn(56); cols.RelativeColumn(1.6f); cols.RelativeColumn(1.6f);
                cols.ConstantColumn(52); cols.RelativeColumn(1.8f); cols.ConstantColumn(58); cols.ConstantColumn(50);
            });
            t.Header(h =>
            {
                foreach (var title in new[] { "Ticket", "Título", "Creado", "Área solicitante", "Categoría", "Prioridad", "Agente", "Antigüedad", "Estado" })
                    h.Cell().Element(HeaderCell).Text(title);
            });
            foreach (var x in tickets.Take(MaxTicketRows))
            {
                t.Cell().Element(BodyCell).Text(x.Number).FontSize(7.5f);
                t.Cell().Element(BodyCell).Text(x.Title).ClampLines(2);
                t.Cell().Element(BodyCell).Text(x.CreatedAt.ToString("dd/MM/yy"));
                t.Cell().Element(BodyCell).Text(x.Area).ClampLines(2);
                t.Cell().Element(BodyCell).Text(x.Category).ClampLines(2);
                t.Cell().Element(BodyCell).Text(x.Priority);
                t.Cell().Element(BodyCell).Text(x.Agent).FontColor(x.Agent == "Sin asignar" ? "#C9483F" : Ink).ClampLines(2);
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Duration(x.AgeSeconds));
                t.Cell().Element(BodyCell).Text(x.Overdue ? "Atrasado" : "En plazo").SemiBold().FontColor(x.Overdue ? "#C9483F" : Efectividad);
            }
        });
    });

    // ---------- Agentes ----------

    private static void AgentsBody(ColumnDescriptor col, AgentsResponse a) => col.Item().Column(s =>
    {
        s.Spacing(8);
        s.Item().Element(x => SectionTitle(x, "Indicadores por agente", "Ordenado por carga asignada"));
        var peak = Math.Max(1, a.Agents.Select(x => x.Assigned).DefaultIfEmpty().Max());
        s.Item().Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.6f); cols.RelativeColumn(1.6f); cols.ConstantColumn(42); cols.ConstantColumn(42); cols.ConstantColumn(42);
                cols.ConstantColumn(50); cols.ConstantColumn(56); cols.ConstantColumn(56);
            });
            t.Header(h =>
            {
                foreach (var title in new[] { "Agente", "Carga", "Resueltos", "Abiertos", "Atrasados", "Eficacia", "Efectividad", "Tiempo típico" })
                    h.Cell().Element(HeaderCell).Text(title);
            });
            foreach (var x in a.Agents)
            {
                var i = x.Indicators;
                t.Cell().Element(BodyCell).Text(x.Name).FontColor(x.AgentId is null ? Muted : Ink);
                t.Cell().Element(BodyCell).Element(cell => HBar(cell, null, x.Assigned, peak, Faint, x.Assigned.ToString()));
                t.Cell().Element(BodyCell).AlignRight().Text(x.Resolved.ToString());
                t.Cell().Element(BodyCell).AlignRight().Text(x.Open.ToString());
                t.Cell().Element(BodyCell).AlignRight().Text(x.Overdue.ToString()).FontColor(x.Overdue > 0 ? "#C9483F" : Ink);
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Pct(i.Eficacia.Percent));
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Pct(i.Efectividad.Percent));
                t.Cell().Element(BodyCell).AlignRight().Text(ReportText.Duration(i.ResolutionTime.MedianSeconds));
            }
        });
    });

    // ---------- Método ----------

    private static void Method(IContainer c, ReportContent r) => c.Column(col =>
    {
        col.Spacing(6);
        col.Item().Element(x => SectionTitle(x, "Método y calidad de los datos", "Cómo leer las cifras de este informe"));

        if (r.Header.Kind != "atencion")
            col.Item().PreventPageBreak().Table(t =>
            {
                t.ColumnsDefinition(cols => { cols.RelativeColumn(2.2f); cols.ConstantColumn(56); cols.ConstantColumn(56); cols.ConstantColumn(52); cols.ConstantColumn(48); cols.RelativeColumn(1.6f); });
                t.Header(h =>
                {
                    foreach (var title in new[] { "Indicador", "Actual", "Anterior", "Variación", "Meta", "Estado" })
                        h.Cell().Element(HeaderCell).Text(title);
                });
                foreach (var k in r.Kpis)
                {
                    t.Cell().Element(BodyCell).Text(k.Name);
                    t.Cell().Element(BodyCell).AlignRight().Text(FormatKpi(k.Unit, k.Current)).SemiBold();
                    t.Cell().Element(BodyCell).AlignRight().Text(FormatKpi(k.Unit, k.Previous));
                    t.Cell().Element(BodyCell).AlignRight().Text(ReportText.VariationText(k));
                    t.Cell().Element(BodyCell).AlignRight().Text(FormatKpi(k.Unit, k.Goal));
                    var status = ReportText.StatusColor(k.Status);
                    t.Cell().Element(BodyCell).Text(ReportText.StatusText(k.Status)).FontColor(status?.Ink ?? Muted);
                }
            });

        col.Item().PaddingTop(4).Column(defs =>
        {
            defs.Spacing(3);
            foreach (var (name, formula) in ReportText.Definitions)
                defs.Item().Text(t => { t.Span($"{name}: ").SemiBold(); t.Span(formula).FontColor(Ink2); });
            defs.Item().Text(t =>
            {
                t.Span("Objetivos por prioridad: ").SemiBold();
                t.Span(string.Join(" · ", r.Targets.Select(x => $"{x.Priority} {x.Hours:0.#} h"))).FontColor(Ink2);
                t.Span(". Znuny no tiene SLA configurado: objetivos y metas son referencias internas ajustables.").FontColor(Ink2);
            });
        });

        var issues = r.Quality?.Checks.Where(q => q.Severity != "ok").ToList() ?? [];
        var notices = r.Notices.Where(n => n.Severity != "warning").ToList();
        if (issues.Count > 0 || notices.Count > 0)
            col.Item().PaddingTop(4).Background(Sunken).CornerRadius(6).Padding(8).Column(q =>
            {
                q.Spacing(3);
                q.Item().Text("Limitaciones de los datos").SemiBold();
                foreach (var check in issues)
                    q.Item().Text(t =>
                    {
                        t.Span($"{check.Title}{(check.Affected is { } n ? $" ({n} de {check.OutOf})" : "")}: ").SemiBold().FontColor(check.Severity == "warning" ? "#9A6408" : Ink);
                        t.Span(check.Detail).FontColor(Ink2);
                    });
                foreach (var n in notices.Where(n => issues.All(i => i.Code != n.Code)))
                    q.Item().Text(n.Message).FontColor(Ink2);
            });
    });

    // ---------- Piezas ----------

    private static void SectionTitle(IContainer c, string title, string subtitle, string? color = null) =>
        c.BorderBottom(0.5f).BorderColor(Line).PaddingBottom(4).Row(row =>
        {
            if (color is not null) row.AutoItem().PaddingTop(4).PaddingRight(6).Width(8).Height(8).Background(color).CornerRadius(4);
            row.AutoItem().Text(title).FontSize(13).Bold();
            row.RelativeItem().PaddingLeft(8).PaddingTop(3).Text(subtitle).FontSize(8.5f).FontColor(Muted);
        });

    private static void Legend(IContainer c, (string Label, string Color)[] items) => c.Row(row =>
    {
        row.Spacing(12);
        foreach (var (label, color) in items)
            row.AutoItem().Row(item => { item.AutoItem().PaddingTop(2.5f).Width(7).Height(7).Background(color); item.AutoItem().PaddingLeft(4).Text(label).FontSize(8).FontColor(Muted); });
    });

    /// <summary>Barra horizontal con etiqueta a la izquierda y valor a la derecha (el valor nunca va dentro de la barra).</summary>
    private static void HBar(IContainer c, string? label, double value, double max, string color, string valueText) => c.Row(row =>
    {
        if (label is not null) row.RelativeItem(1.6f).Text(label).FontColor(Ink2).ClampLines(1);
        row.RelativeItem(1.4f).PaddingTop(3.5f).PaddingHorizontal(6).Height(5).Layers(l =>
        {
            l.PrimaryLayer().Background(Sunken).CornerRadius(2.5f);
            l.Layer().Row(bar =>
            {
                var share = (float)Math.Clamp(value / max, 0, 1);
                if (share > 0) bar.RelativeItem(share).Background(color).CornerRadius(2.5f);
                if (share < 1) bar.RelativeItem(1 - share);
            });
        });
        row.ConstantItem(label is null ? 26 : 72).AlignRight().Text(valueText).SemiBold().FontSize(8);
    });

    private static IContainer HeaderCell(IContainer c) =>
        c.BorderBottom(0.75f).BorderColor(Faint).PaddingVertical(4).PaddingHorizontal(3).DefaultTextStyle(t => t.FontSize(7.5f).SemiBold().FontColor(Muted));

    private static IContainer BodyCell(IContainer c) =>
        c.ShowEntire().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(3.5f).PaddingHorizontal(3).DefaultTextStyle(t => t.FontSize(8));

    private static string FormatKpi(KpiUnit unit, double? value) => unit switch
    {
        KpiUnit.Percent => ReportText.Pct(value),
        KpiUnit.Duration => ReportText.Duration(value),
        _ => value is { } v ? v.ToString("#,##0") : "—",
    };
}
