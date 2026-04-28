create table if not exists matches (
    match_id text primary key,
    status text not null,
    current_tick bigint not null default 0,
    rule_version text not null default 'rules-260330',
    card_catalog_version text not null default 'official-2026-04-27',
    seed text null,
    winner_player_id text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists match_players (
    match_id text not null references matches(match_id) on delete cascade,
    player_id text not null,
    seat text not null,
    reconnect_token_hash text null,
    connection_state text not null default 'DISCONNECTED',
    joined_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (match_id, player_id)
);

create table if not exists command_log (
    id bigserial primary key,
    match_id text not null references matches(match_id) on delete cascade,
    player_id text not null,
    client_intent_id text not null,
    command_type text not null,
    started_tick bigint not null,
    completed_tick bigint not null,
    accepted boolean not null,
    error_message text null,
    payload jsonb not null,
    received_at timestamptz not null default now(),
    unique (match_id, player_id, client_intent_id)
);

create table if not exists game_events (
    id bigserial primary key,
    match_id text not null references matches(match_id) on delete cascade,
    event_tick bigint not null,
    event_order integer not null,
    event_type text not null,
    visibility text not null default 'PUBLIC',
    payload jsonb not null,
    created_at timestamptz not null default now(),
    unique (match_id, event_tick, event_order)
);

create table if not exists snapshots (
    id bigserial primary key,
    match_id text not null references matches(match_id) on delete cascade,
    player_id text not null,
    snapshot_tick bigint not null,
    payload jsonb not null,
    created_at timestamptz not null default now(),
    unique (match_id, player_id, snapshot_tick)
);

create table if not exists action_prompts (
    id bigserial primary key,
    match_id text not null references matches(match_id) on delete cascade,
    player_id text not null,
    prompt_tick bigint not null,
    payload jsonb not null,
    created_at timestamptz not null default now(),
    unique (match_id, player_id, prompt_tick)
);

create table if not exists official_cards (
    official_id bigint primary key,
    card_no text not null,
    card_name text not null,
    card_category text not null,
    functional_unit_id text null,
    payload jsonb not null,
    catalog_version text not null,
    imported_at timestamptz not null default now()
);

create table if not exists functional_units (
    functional_unit_id text primary key,
    representative_no text not null,
    name text not null,
    category text not null,
    group_size integer not null,
    signature text not null,
    payload jsonb not null,
    catalog_version text not null,
    imported_at timestamptz not null default now()
);

create index if not exists idx_command_log_match_tick on command_log(match_id, completed_tick);
create index if not exists idx_game_events_match_tick on game_events(match_id, event_tick, event_order);
create index if not exists idx_snapshots_match_player_tick on snapshots(match_id, player_id, snapshot_tick);
create index if not exists idx_action_prompts_match_player_tick on action_prompts(match_id, player_id, prompt_tick);
create index if not exists idx_official_cards_functional_unit on official_cards(functional_unit_id);
