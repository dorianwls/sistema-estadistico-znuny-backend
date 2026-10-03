using ZnunyStats.Api.Modules.Agents.GetAgents;

namespace ZnunyStats.Api.Modules.Agents;

public static class AgentsModule
{
    public static void MapAgentsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agents").WithTags("Agents");

        group.MapGetAgents();
    }
}
