using FluentValidation;

using ZnunyStats.Api.Common;

namespace ZnunyStats.Api.Modules.Shared;

public sealed class TicketQueryValidator : AbstractValidator<TicketQuery>
{
    public TicketQueryValidator(ZnunyClock clock)
    {
        // Sin "to" el período termina hoy, así que también se valida contra la fecha de hoy.
        RuleFor(q => q.From)
            .Must((q, from) => from <= (q.To ?? clock.Today))
            .When(q => q.From is not null)
            .WithMessage("La fecha inicial no puede ser posterior a la final.");

        RuleFor(q => q.From)
            .Must((q, from) => (q.To ?? clock.Today).DayNumber - from!.Value.DayNumber <= Period.MaxSpanDays)
            .When(q => q.From is not null)
            .WithMessage("El período máximo es de un año.");

        RuleFor(q => q.AgentId).GreaterThan(0);
        RuleFor(q => q.PriorityId).GreaterThan(0);
    }
}
