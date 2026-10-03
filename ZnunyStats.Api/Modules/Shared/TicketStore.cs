using System.Globalization;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using ZnunyStats.Api.Common;
using ZnunyStats.Api.Database;
using ZnunyStats.Domain.Entities;

namespace ZnunyStats.Api.Modules.Shared;

/// <summary>Instantánea de la fuente: tickets clasificados más datos de contexto.</summary>
public sealed record Snapshot(
    IReadOnlyList<TicketRecord> Tickets,
    IReadOnlyDictionary<int, string> AgentNames,
    SourceCapabilities Capabilities,
    DateTime LoadedAtUtc);

/// <summary>
/// Lee Znuny y transforma cada fila en un <see cref="TicketRecord"/>.
/// El volumen actual (cientos de tickets) cabe holgadamente en memoria; se cachea unos segundos
/// para no golpear la base en cada petición del dashboard.
/// </summary>
public sealed class TicketStore(ZnunyQueries queries, IMemoryCache cache, IOptions<StatsOptions> options, TimeProvider clock)
{
    private const string CacheKey = "znuny-snapshot";
    private readonly StatsOptions _o = options.Value;

    public async Task<Snapshot> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Snapshot? cached) && cached is not null)
            return cached;

        var users = await queries.GetUsersAsync(ct);
        var facts = await queries.GetTicketFactsAsync(_o, ct);
        var capabilities = await queries.GetCapabilitiesAsync(ct);

        var names = users
            .Where(u => !_o.SystemUserIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => DisplayName(u));

        var snapshot = new Snapshot(
            facts.Select(f => ToRecord(f, names)).ToList(),
            names,
            capabilities,
            clock.GetUtcNow().UtcDateTime);

        cache.Set(CacheKey, snapshot, TimeSpan.FromSeconds(_o.CacheSeconds));
        return snapshot;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    private TicketRecord ToRecord(TicketFact f, IReadOnlyDictionary<int, string> names)
    {
        var (unit, category) = SplitQueue(f.QueueName);
        var agentId = ResponsibleAgent(f);

        return new TicketRecord
        {
            Id = f.TicketId,
            Number = f.TicketNumber,
            Title = f.Title,
            CreatedAt = f.CreatedAt,
            Queue = f.QueueName,
            Unit = unit,
            Category = category,
            LocationKey = LocationFor(f.QueueName),
            PriorityId = f.PriorityId,
            Priority = PriorityLabel(f.PriorityName),
            Status = StatusFor(f.StateType, f.StateName),
            Area = string.IsNullOrWhiteSpace(f.CustomerArea) ? "Sin área registrada" : TitleCase(f.CustomerArea),
            AgentId = agentId,
            AgentName = agentId is { } id && names.TryGetValue(id, out var n) ? n : "Sin asignar",
            CreatedByAgent = f.CreatedByAgent,
            FirstAttentionAt = f.FirstAttentionAt,
            ResolvedAt = f.ResolvedAt,
            ReopenedAt = f.ReopenedAt,
            BulkClosed = f.BulkClosed,
            Transfers = f.Transfers,
            AgentReplies = f.AgentReplies,
            Target = TimeSpan.FromHours(
                _o.ResolutionTargetHours.TryGetValue(f.PriorityName, out var hours) ? hours : _o.DefaultResolutionTargetHours),
        };
    }

    /// <summary>
    /// Regla única de atribución: quien era propietario al primer cierre exitoso
    /// (si era una cuenta de sistema, quien cerró); si no se ha cerrado, el propietario actual.
    /// </summary>
    private int? ResponsibleAgent(TicketFact f)
    {
        int?[] candidates = f.ResolvedAt is null
            ? [f.CurrentOwnerId]
            : [f.ResolvedOwnerId, f.ResolvedById];
        return candidates.FirstOrDefault(id => id is not null && !_o.SystemUserIds.Contains(id.Value));
    }

    private string StatusFor(string stateType, string stateName) => stateType switch
    {
        "new" => TicketStatus.New,
        "open" => TicketStatus.Open,
        "pending reminder" or "pending auto" => TicketStatus.Pending,
        "closed" => _o.SuccessStates.Contains(stateName) ? TicketStatus.Resolved : TicketStatus.ClosedUnsuccessful,
        "merged" => TicketStatus.Merged,
        "removed" => TicketStatus.Removed,
        _ => TicketStatus.Open,
    };

    private string LocationFor(string queue) =>
        _o.Locations.FirstOrDefault(l => l.QueueMatch != "" && queue.Contains(l.QueueMatch, StringComparison.OrdinalIgnoreCase))?.Key
        ?? _o.Locations.FirstOrDefault(l => l.QueueMatch == "")?.Key
        ?? "";

    private static (string Unit, string Category) SplitQueue(string queue)
    {
        var parts = queue.Split("::", 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "General");
    }

    /// <summary>"5 very high" → "Muy alta".</summary>
    public static string PriorityLabel(string znunyName) => znunyName switch
    {
        "5 very high" => "Muy alta",
        "4 high" => "Alta",
        "3 normal" => "Normal",
        "2 low" => "Baja",
        "1 very low" => "Muy baja",
        _ => znunyName,
    };

    private static string DisplayName(UserRow u)
    {
        var name = $"{u.FirstName} {u.LastName}".Trim();
        return TitleCase(string.IsNullOrEmpty(name) ? u.Login : name);
    }

    private static readonly HashSet<string> Connectors = ["de", "del", "la", "las", "los", "y", "e"];

    /// <summary>Normaliza nombres escritos todo en mayúsculas o minúsculas; respeta los que ya vienen bien escritos.</summary>
    private static string TitleCase(string text)
    {
        if (text != text.ToUpperInvariant() && text != text.ToLowerInvariant())
            return text;

        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select((w, i) =>
            i > 0 && Connectors.Contains(w) ? w : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w)));
    }
}
