using ZnunyStats.Domain.ReadModels;

namespace ZnunyStats.Api.Modules.Tickets.GetTickets;

public sealed record TicketPage(int Total, int Page, int PageSize, IReadOnlyList<TicketRow> Items);
