using ZnunyStats.Api.Modules.Tickets.GetTickets;
using ZnunyStats.Api.Modules.Tickets.GetTicketTimeline;

namespace ZnunyStats.Api.Modules.Tickets;

public static class TicketsModule
{
    public static void MapTicketsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tickets").WithTags("Tickets");

        group.MapGetTickets();
        group.MapGetTicketTimeline();
    }
}
