alter table matches
    add column if not exists last_event_sequence bigint not null default 0;

alter table command_log
    add column if not exists started_event_sequence bigint not null default 0,
    add column if not exists completed_event_sequence bigint not null default 0;

alter table snapshots
    add column if not exists last_event_sequence bigint not null default 0;

alter table action_prompts
    add column if not exists last_event_sequence bigint not null default 0;

alter table game_events
    add column if not exists event_sequence bigint;

with numbered as (
    select id,
           row_number() over (
               partition by match_id
               order by event_tick, event_order, id
           ) as event_sequence
    from game_events
    where event_sequence is null
)
update game_events
set event_sequence = numbered.event_sequence
from numbered
where game_events.id = numbered.id;

alter table game_events
    alter column event_sequence set not null;

create unique index if not exists ux_game_events_match_sequence
    on game_events(match_id, event_sequence);

create index if not exists idx_command_log_match_sequence
    on command_log(match_id, completed_event_sequence);

create index if not exists idx_game_events_match_sequence
    on game_events(match_id, event_sequence);
