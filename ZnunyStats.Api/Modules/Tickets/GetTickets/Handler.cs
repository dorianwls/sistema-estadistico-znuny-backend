using ZnunyStats.Api.Modules.Shared;
using ZnunyStats.Domain.Entities;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Tickets.GetTickets;

/// <summary>Listado de tickets de la población. Lo usan el endpoint y los reportes.</summary>
public static class GetTicketsHandler
{
    public const string AttentionView = "attention";
    public const int MaxPageSize = 500;

    /// <param name="view">"all" (más recientes primero) o "attention" (abiertos, atrasados primero).</param>
    public static async Task<TicketPage> ExecuteAsync(TicketQuery q, string? view, int page, int pageSize, TicketAnalytics a, CancellationToken ct)
    {
        var list = await SelectAsync(q, view, a, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(a.ToRow).ToList();
        return new TicketPage(list.Count, page, pageSize, items);
    }

    /// <summary>Todas las filas, sin paginar (para los reportes).</summary>
    public static async Task<IReadOnlyList<TicketRow>> AllAsync(TicketQuery q, string? view, TicketAnalytics a, CancellationToken ct) =>
        (await SelectAsync(q, view, a, ct)).Select(a.ToRow).ToList();

    private static async Task<List<TicketRecord>> SelectAsync(TicketQuery q, string? view, TicketAnalytics a, CancellationToken ct)
    {
        var snap = await a.GetSnapshotAsync(ct);
        IEnumerable<TicketRecord> tickets = a.Population(snap, q, a.ResolvePeriod(q));
        var now = a.NowUtc;

        tickets = view == AttentionView
            ? tickets.Where(t => t.IsOpen).OrderByDescending(t => t.IsOverdue(now)).ThenBy(t => t.CreatedAt)
            : tickets.OrderByDescending(t => t.CreatedAt);
        return tickets.ToList();
    }
}
