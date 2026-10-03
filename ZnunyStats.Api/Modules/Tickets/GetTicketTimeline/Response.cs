using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Tickets.GetTicketTimeline;

public sealed record TimelineEvent(DateTimeOffset At, string Kind, string Description, string Actor, string State, string Queue);

public sealed record TicketTimeline(TicketRow Ticket, IReadOnlyList<TimelineEvent> Events);
