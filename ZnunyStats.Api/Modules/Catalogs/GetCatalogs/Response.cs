using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Catalogs.GetCatalogs;

public sealed record Option(string Value, string Label);

public sealed record CatalogsResponse(
    IReadOnlyList<Option> Areas,
    IReadOnlyList<Option> Units,
    IReadOnlyList<Option> Categories,
    IReadOnlyList<Option> Priorities,
    IReadOnlyList<Option> Statuses,
    IReadOnlyList<Option> Agents,
    IReadOnlyList<Option> Locations,
    DateOnly? FirstTicketDate,
    DateOnly? LastTicketDate,
    DateOnly DefaultFrom,
    DateOnly DefaultTo,
    IReadOnlyList<TargetInfo> Targets,
    int ReopenWindowDays);
