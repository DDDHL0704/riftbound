2026-06-16 20:15 CST

Stage 4D-223AZ recovery spectator battlefield-task missing/null-payload count-drift validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- Touched code: `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- `ValidateSpectatorBattlefieldTaskPayloads` now reports spectator replay-frame timing battlefield-task count `0` versus non-empty authoritative battlefield tasks when `battlefieldTasks` is missing or null, while preserving the required-payload error.
- Added/renamed paired tests:
  - `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTasksMissingPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTasksMissingPayloadWithCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTasksNullPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTasksNullPayloadWithCountMismatch`
- Empty-authoritative missing/null payloads still omit count mismatch; contested-battlefield authoritative missing/null payloads now emit both required-payload and count-mismatch diagnostics.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 316.4-316.5 for battle and spell-duel task creation context.
- Latest core rules 323.9-323.14 for pending battlefield battle/spell-duel task cleanup transitions.
- Latest core rules 334-335 for task processing and HOT/FEPR boundaries.
- Latest core rules 454-455 and 458 for battle start and battle-step context.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before docs sync; it was clean at `01364ee2`, 21 commits behind current `main` after the code commit and with no commits ahead of `main`.
- `rule-audit-remaining-20260615` had no new commits ahead of `main` before code commit or docs sync.
- Root PDF rule files remained present.

Validation passed:
- Focused missing/null payload pair: `4/4`.
- Changed-class `MatchRecoveryTests`: `1958/1958`.
- Adjacent BattlefieldTask/BattlefieldTasks/SpectatorReplayTiming/Recovery filter: `1975/1975`.
- Backend full via `Riftbound.slnx`: `8288/8288`.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

Code commit:
- `50afb35c fix: report recovery battlefield task payload count drift`

Non-goals:
- Does not change valid recovery replay behavior.
- Does not change battlefield task creation, spell-duel start, battle start, cleanup ordering, battle-step runtime behavior, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
