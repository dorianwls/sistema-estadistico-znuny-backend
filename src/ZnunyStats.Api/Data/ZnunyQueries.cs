using Dapper;
using Npgsql;

namespace ZnunyStats.Api.Data;

/// <summary>
/// Todas las consultas a la base de Znuny. Solo lectura: la cadena de conexión fuerza
/// default_transaction_read_only=on y aquí no existe ninguna sentencia de escritura.
/// </summary>
public sealed class ZnunyQueries(NpgsqlDataSource db)
{
    /// <summary>
    /// Una fila por ticket con los hitos necesarios para los indicadores.
    /// Los hitos se reconstruyen desde ticket_history porque Znuny no guarda fecha de resolución.
    /// </summary>
    private const string TicketFactsSql = """
        with history as (
            select h.id, h.ticket_id, h.state_id, h.owner_id, h.create_by, h.create_time, ht.name as type_name
            from ticket_history h
            join ticket_history_type ht on ht.id = h.history_type_id
        ),
        -- Primer paso a un estado de éxito ("closed successful").
        first_success as (
            select distinct on (h.ticket_id) h.ticket_id, h.id, h.create_time, h.create_by, h.owner_id
            from history h
            join ticket_state s on s.id = h.state_id
            where h.type_name = 'StateUpdate' and s.name = any(@SuccessStates)
            order by h.ticket_id, h.create_time, h.id
        ),
        -- Primer regreso a un estado activo después de esa resolución.
        reopened as (
            select fs.ticket_id, min(h.create_time) as reopened_at
            from first_success fs
            join history h on h.ticket_id = fs.ticket_id and h.type_name = 'StateUpdate' and h.id > fs.id
            join ticket_state s on s.id = h.state_id
            join ticket_state_type st on st.id = s.type_id
            where st.name in ('new', 'open', 'pending reminder', 'pending auto')
            group by fs.ticket_id
        ),
        -- Primera acción de una persona (no usuario de sistema) sobre el ticket.
        first_attention as (
            select h.ticket_id, min(h.create_time) as attended_at
            from history h
            where h.create_by <> all(@SystemUserIds)
              and h.type_name in ('Lock', 'OwnerUpdate', 'Move', 'StateUpdate', 'AddNote', 'SendAnswer',
                                  'EmailAgent', 'PhoneCallAgent', 'PriorityUpdate', 'Forward', 'Bounce')
            group by h.ticket_id
        ),
        -- Tickets que abrió un agente (llamada, atención presencial): no hubo espera hasta la primera atención.
        created_by_agent as (
            select distinct ticket_id from history
            where type_name = 'NewTicket' and create_by <> all(@SystemUserIds)
        ),
        -- Cierres hechos con la acción masiva de Znuny: su duración no refleja trabajo real.
        bulk_closed as (
            select fs.ticket_id
            from first_success fs
            where exists (
                select 1 from history b
                where b.ticket_id = fs.ticket_id and b.type_name = 'Bulk' and b.create_by = fs.create_by
                  and b.create_time between fs.create_time - make_interval(mins => @BulkCloseWindowMinutes) and fs.create_time)
        ),
        transfers as (
            select ticket_id, count(*) as total from history where type_name = 'Move' group by ticket_id
        ),
        agent_replies as (
            select a.ticket_id, count(*) as total
            from article a
            join article_sender_type ast on ast.id = a.article_sender_type_id
            where ast.name = 'agent' and a.is_visible_for_customer = 1
            group by a.ticket_id
        )
        select t.id                                          as TicketId,
               t.tn                                          as TicketNumber,
               t.title                                       as Title,
               t.create_time at time zone @SourceTimeZone    as CreatedAt,
               q.name                                        as QueueName,
               p.id                                          as PriorityId,
               p.name                                        as PriorityName,
               s.name                                        as StateName,
               st.name                                       as StateType,
               t.user_id                                     as CurrentOwnerId,
               coalesce(cc_user.name, cc_ticket.name)          as CustomerArea,
               (cba.ticket_id is not null)                   as CreatedByAgent,
               fa.attended_at at time zone @SourceTimeZone   as FirstAttentionAt,
               fs.create_time at time zone @SourceTimeZone   as ResolvedAt,
               fs.create_by                                  as ResolvedById,
               fs.owner_id                                   as ResolvedOwnerId,
               (bc.ticket_id is not null)                    as BulkClosed,
               r.reopened_at at time zone @SourceTimeZone    as ReopenedAt,
               coalesce(tr.total, 0)::int                    as Transfers,
               coalesce(ar.total, 0)::int                    as AgentReplies
        from ticket t
        join queue q on q.id = t.queue_id
        join ticket_priority p on p.id = t.ticket_priority_id
        join ticket_state s on s.id = t.ticket_state_id
        join ticket_state_type st on st.id = s.type_id
        left join customer_user cu on cu.login = t.customer_user_id
        left join customer_company cc_user on cc_user.customer_id = cu.customer_id
        left join customer_company cc_ticket on cc_ticket.customer_id = t.customer_id
        left join first_success fs on fs.ticket_id = t.id
        left join reopened r on r.ticket_id = t.id
        left join first_attention fa on fa.ticket_id = t.id
        left join created_by_agent cba on cba.ticket_id = t.id
        left join bulk_closed bc on bc.ticket_id = t.id
        left join transfers tr on tr.ticket_id = t.id
        left join agent_replies ar on ar.ticket_id = t.id
        where q.name <> all(@ExcludedQueues)
          and not (t.title like any(@ExcludedTitlePatterns))
        """;

    public async Task<IReadOnlyList<TicketFact>> GetTicketFactsAsync(StatsOptions o, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var args = new
        {
            o.SuccessStates,
            o.SystemUserIds,
            o.ExcludedQueues,
            o.ExcludedTitlePatterns,
            o.BulkCloseWindowMinutes,
            o.SourceTimeZone,
        };
        var rows = await conn.QueryAsync<TicketFact>(new CommandDefinition(TicketFactsSql, args, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<UserRow>> GetUsersAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        const string sql = "select id as Id, login as Login, first_name as FirstName, last_name as LastName from users";
        var rows = await conn.QueryAsync<UserRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Configuración que condiciona qué se puede medir (SLA, registro de tiempo).</summary>
    public async Task<SourceCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        const string sql = """
            select (select count(*) from sla where valid_id = 1)::int as ActiveSlas,
                   (select count(*) from time_accounting)::int        as TimeAccountingEntries
            """;
        return await conn.QuerySingleAsync<SourceCapabilities>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<HistoryRow>> GetTicketHistoryAsync(long ticketId, string sourceTimeZone, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        const string sql = """
            select h.id                                       as Id,
                   h.create_time at time zone @SourceTimeZone as At,
                   ht.name                                    as Type,
                   h.name                                     as Detail,
                   s.name                                     as State,
                   q.name                                     as Queue,
                   h.create_by                                as ActorId
            from ticket_history h
            join ticket_history_type ht on ht.id = h.history_type_id
            join ticket_state s on s.id = h.state_id
            join queue q on q.id = h.queue_id
            where h.ticket_id = @TicketId and ht.name <> 'Misc'
            order by h.create_time, h.id
            """;
        var rows = await conn.QueryAsync<HistoryRow>(
            new CommandDefinition(sql, new { TicketId = ticketId, SourceTimeZone = sourceTimeZone }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<bool> PingAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("select 1", cancellationToken: ct)) == 1;
    }
}

public sealed class TicketFact
{
    public long TicketId { get; init; }
    public string TicketNumber { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string QueueName { get; init; } = "";
    public int PriorityId { get; init; }
    public string PriorityName { get; init; } = "";
    public string StateName { get; init; } = "";
    public string StateType { get; init; } = "";
    public int CurrentOwnerId { get; init; }
    public string? CustomerArea { get; init; }
    public bool CreatedByAgent { get; init; }
    public DateTime? FirstAttentionAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public int? ResolvedById { get; init; }
    public int? ResolvedOwnerId { get; init; }
    public bool BulkClosed { get; init; }
    public DateTime? ReopenedAt { get; init; }
    public int Transfers { get; init; }
    public int AgentReplies { get; init; }
}

public sealed class UserRow
{
    public int Id { get; init; }
    public string Login { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}

public sealed class SourceCapabilities
{
    public int ActiveSlas { get; init; }
    public int TimeAccountingEntries { get; init; }
}

public sealed class HistoryRow
{
    public long Id { get; init; }
    public DateTime At { get; init; }
    public string Type { get; init; } = "";
    public string Detail { get; init; } = "";
    public string State { get; init; } = "";
    public string Queue { get; init; } = "";
    public int ActorId { get; init; }
}
