create table if not exists state_snapshots (
    id bigserial primary key,
    match_id text not null references matches(match_id) on delete cascade,
    state_tick bigint not null,
    last_event_sequence bigint not null default 0,
    ruleset_version text not null default 'rules-260330',
    faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    rules_evidence jsonb not null default '[]'::jsonb,
    payload jsonb not null,
    created_at timestamptz not null default now(),
    unique (match_id, state_tick, last_event_sequence)
);

create index if not exists idx_state_snapshots_match_sequence
    on state_snapshots(match_id, last_event_sequence);
