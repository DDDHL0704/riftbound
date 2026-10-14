-- Existing development games deliberately remain unbound and cannot resume.
-- Only newly recorded games receive the actual running input identity.
alter table matches add column if not exists rule_inputs jsonb;
alter table command_log add column if not exists rule_inputs jsonb;
alter table game_events add column if not exists rule_inputs jsonb;
alter table state_snapshots add column if not exists rule_inputs jsonb;
alter table snapshots add column if not exists rule_inputs jsonb;
alter table action_prompts add column if not exists rule_inputs jsonb;
