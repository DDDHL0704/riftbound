create table if not exists player_identity (
    handle text primary key,
    key_hash text not null,
    claimed_at timestamptz not null default now()
);
