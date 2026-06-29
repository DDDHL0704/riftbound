create table if not exists match_results (
    match_id text primary key references matches(match_id) on delete cascade,
    winner_player_id text not null,
    finished_at timestamptz not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists match_result_players (
    match_id text not null references match_results(match_id) on delete cascade,
    player_id text not null,
    seat text not null,
    score integer not null check (score >= 0),
    won boolean not null,
    primary key (match_id, player_id)
);

create index if not exists idx_match_results_finished_at on match_results(finished_at desc);
create index if not exists idx_match_result_players_player on match_result_players(player_id, match_id);
