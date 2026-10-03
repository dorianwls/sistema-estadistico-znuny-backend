using Xunit;

using ZnunyStats.Domain.Entities;
using ZnunyStats.Domain.Metrics;

namespace ZnunyStats.UnitTests.Metrics;

public class KpisTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TicketRecord Ticket(
        string status = TicketStatus.Resolved,
        double? resolvedAfterHours = 2,
        double targetHours = 24,
        double createdHoursAgo = 240,
        bool bulk = false,
        double? reopenedAfterResolutionDays = null,
        double? attendedAfterHours = 1,
        bool createdByAgent = false)
    {
        var created = Now.AddHours(-createdHoursAgo);
        DateTime? resolved = resolvedAfterHours is { } r ? created.AddHours(r) : null;
        return new TicketRecord
        {
            Id = 1, Number = "1", Title = "t", CreatedAt = created, Queue = "Q", Unit = "Q", Category = "General",
            LocationKey = "X", PriorityId = 3, Priority = "Normal", Status = status, Area = "A", AgentId = 10, AgentName = "Ana",
            CreatedByAgent = createdByAgent,
            FirstAttentionAt = attendedAfterHours is { } a ? created.AddHours(a) : null,
            ResolvedAt = resolved,
            ReopenedAt = resolved is { } res && reopenedAfterResolutionDays is { } d ? res.AddDays(d) : null,
            BulkClosed = bulk, Transfers = 0, AgentReplies = 1, Target = TimeSpan.FromHours(targetHours),
        };
    }

    [Fact]
    public void Eficacia_es_resueltos_sobre_tickets_a_cargo()
    {
        TicketRecord[] tickets =
        [
            Ticket(),
            Ticket(bulk: true),
            Ticket(status: TicketStatus.Open, resolvedAfterHours: null),
            Ticket(status: TicketStatus.ClosedUnsuccessful, resolvedAfterHours: null),
        ];

        var k = Kpis.Eficacia(tickets);

        Assert.Equal(2, k.Numerator);
        Assert.Equal(4, k.Denominator);
        Assert.Equal(50, k.Percent);
    }

    [Fact]
    public void Eficacia_sin_tickets_es_null_y_no_cero()
    {
        Assert.Null(Kpis.Eficacia([]).Percent);
    }

    [Fact]
    public void Efectividad_exige_resolver_a_tiempo_y_sin_reabrir()
    {
        TicketRecord[] tickets =
        [
            Ticket(resolvedAfterHours: 2),                                     // éxito
            Ticket(resolvedAfterHours: 30),                                    // fuera de objetivo
            Ticket(resolvedAfterHours: 2, reopenedAfterResolutionDays: 3),     // reabierto
            Ticket(status: TicketStatus.Open, resolvedAfterHours: null),        // abierto y vencido → fracaso
            Ticket(status: TicketStatus.Open, resolvedAfterHours: null, createdHoursAgo: 1), // aún en plazo → pendiente
            Ticket(bulk: true),                                                // cierre masivo → excluido
        ];

        var k = Kpis.Efectividad(tickets, Now, reopenWindowDays: 7);

        Assert.Equal(1, k.Numerator);
        Assert.Equal(4, k.Denominator);
        Assert.Equal(1, k.Pending);
        Assert.Equal(1, k.Excluded);
        Assert.Equal(25, k.Percent);
    }

    [Fact]
    public void Clasificacion_explica_cada_caso_de_efectividad()
    {
        Outcome Classify(TicketRecord t) => Kpis.Classify(t, Now, reopenWindowDays: 7);

        Assert.Equal(Outcome.OnTime, Classify(Ticket(resolvedAfterHours: 2)));
        Assert.Equal(Outcome.Late, Classify(Ticket(resolvedAfterHours: 30)));
        Assert.Equal(Outcome.Reopened, Classify(Ticket(resolvedAfterHours: 2, reopenedAfterResolutionDays: 3)));
        Assert.Equal(Outcome.Reopened, Classify(Ticket(status: TicketStatus.Open, resolvedAfterHours: 2)));
        Assert.Equal(Outcome.Overdue, Classify(Ticket(status: TicketStatus.Open, resolvedAfterHours: null)));
        Assert.Equal(Outcome.Unsuccessful, Classify(Ticket(status: TicketStatus.ClosedUnsuccessful, resolvedAfterHours: null)));
        Assert.Equal(Outcome.Pending, Classify(Ticket(status: TicketStatus.Open, resolvedAfterHours: null, createdHoursAgo: 1)));
        Assert.Equal(Outcome.Excluded, Classify(Ticket(bulk: true)));
    }

    [Fact]
    public void Reapertura_fuera_de_la_ventana_no_cuenta()
    {
        var k = Kpis.Efectividad([Ticket(resolvedAfterHours: 2, reopenedAfterResolutionDays: 8)], Now, reopenWindowDays: 7);
        Assert.Equal(100, k.Percent);
    }

    [Fact]
    public void Tiempo_de_resolucion_usa_mediana_y_excluye_cierres_masivos()
    {
        TicketRecord[] tickets =
        [
            Ticket(resolvedAfterHours: 1),
            Ticket(resolvedAfterHours: 2),
            Ticket(resolvedAfterHours: 100),
            Ticket(resolvedAfterHours: 0.01, bulk: true),
        ];

        var k = Kpis.ResolutionTime(tickets);

        Assert.Equal(3, k.Sample);
        Assert.Equal(1, k.Excluded);
        Assert.Equal(2 * 3600, k.MedianSeconds);
    }

    [Fact]
    public void Primera_atencion_excluye_tickets_abiertos_por_el_agente()
    {
        TicketRecord[] tickets =
        [
            Ticket(attendedAfterHours: 3),
            Ticket(attendedAfterHours: 0, createdByAgent: true),
            Ticket(attendedAfterHours: 0.1, bulk: true),
        ];

        var k = Kpis.FirstAttentionTime(tickets);

        Assert.Equal(1, k.Sample);
        Assert.Equal(2, k.Excluded);
        Assert.Equal(3 * 3600, k.MedianSeconds);
    }

    [Fact]
    public void Reapertura_solo_evalua_resoluciones_con_ventana_cumplida()
    {
        TicketRecord[] tickets =
        [
            Ticket(resolvedAfterHours: 2, reopenedAfterResolutionDays: 1),
            Ticket(resolvedAfterHours: 2),
            Ticket(resolvedAfterHours: 1, createdHoursAgo: 24), // resuelto hace menos de 7 días → pendiente
        ];

        var k = Kpis.ReopenRate(tickets, Now, windowDays: 7);

        Assert.Equal(1, k.Numerator);
        Assert.Equal(2, k.Denominator);
        Assert.Equal(1, k.Pending);
    }

    [Theory]
    [InlineData(0.5, 2.5)]
    [InlineData(0.9, 3.7)]
    public void Percentil_interpola_como_percentile_cont(double p, double expected)
    {
        Assert.Equal(expected, Kpis.Percentile([1, 2, 3, 4], p), precision: 6);
    }
}
