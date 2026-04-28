alter table matches
    add column if not exists ruleset_version text not null default 'rules-260330',
    add column if not exists faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table command_log
    add column if not exists ruleset_version text not null default 'rules-260330',
    add column if not exists faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table game_events
    add column if not exists ruleset_version text not null default 'rules-260330',
    add column if not exists faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table snapshots
    add column if not exists ruleset_version text not null default 'rules-260330',
    add column if not exists faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table action_prompts
    add column if not exists ruleset_version text not null default 'rules-260330',
    add column if not exists faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table official_cards
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

alter table functional_units
    add column if not exists rules_audit_status text not null default 'NEEDS_RULE_AUDIT',
    add column if not exists rules_evidence jsonb not null default '[]'::jsonb;

create table if not exists oracle_fixtures (
    fixture_id text primary key,
    source text not null,
    ruleset_version text not null default 'rules-260330',
    faq_version text not null default 'official-pdf-faq-set-2026-04-28',
    audit_status text not null default 'NEEDS_RULE_AUDIT',
    rules_evidence jsonb not null default '[]'::jsonb,
    expected jsonb not null,
    legacy_oracle jsonb null,
    payload jsonb not null,
    java_commit text null,
    catalog_version text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists idx_oracle_fixtures_audit_status on oracle_fixtures(audit_status);
create index if not exists idx_oracle_fixtures_ruleset on oracle_fixtures(ruleset_version, faq_version);
