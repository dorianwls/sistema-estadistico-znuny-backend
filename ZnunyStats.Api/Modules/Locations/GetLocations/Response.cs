using ZnunyStats.Domain.Metrics;
using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Locations.GetLocations;

public sealed record LocationSummary(
    string Key,
    string Name,
    double? Latitude,
    double? Longitude,
    int Total,
    int Open,
    int Overdue,
    int Resolved,
    Indicators Indicators,
    IReadOnlyList<CountItem> ByUnit);

public sealed record LocationsResponse(Meta Meta, IReadOnlyList<LocationSummary> Locations);
