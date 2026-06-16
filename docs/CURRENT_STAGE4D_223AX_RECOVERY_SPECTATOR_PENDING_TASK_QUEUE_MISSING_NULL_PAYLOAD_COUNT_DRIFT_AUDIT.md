2026-06-16 18:09 CST

Stage 4D-223AX recovery spectator pending-task-queue missing/null-payload count-drift validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- Touched code: `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- `ValidateSpectatorPendingTaskQueuePayload` now reports spectator replay-frame timing pending-task-queue task-count drift when `pendingTaskQueue` is missing or null and the authoritative pending task queue is non-empty, while preserving the required-payload error.
- Added/renamed paired tests:
  - `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMissingPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMissingPayloadWithTaskCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueNullPayloadWithoutCountMismatch`
  - `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueNullPayloadWithTaskCountMismatch`
- Empty-authoritative missing/null payloads still omit task-count mismatch; non-empty authoritative missing/null payloads now emit both required-payload and task-count mismatch diagnostics.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 142-143 for damage and unit power context.
- Latest core rules 318-323 for cleanup becoming pending, cleanup execution boundaries, repeated cleanup, ordered cleanup tasks and lethal-damage destruction.
- Latest core rules 334-335 for task processing and pending task / FEPR boundaries.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before the slice and before docs sync; it was clean at `01364ee2` with no commits ahead of `main`.
- `rule-audit-remaining-20260615` had no new commits ahead of `main` before code commit or docs sync.
- Root PDF rule files remained present.

Validation passed:
- Focused missing/null payload pair: `4/4`.
- Changed-class `MatchRecoveryTests`: `1954/1954`.
- Adjacent PendingTaskQueue/SpectatorReplayTiming/CleanupTask/Recovery filter: `1970/1970`.
- Backend full: `8284/8284`.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

Code commit:
- `730adcdc fix: report recovery pending task queue payload count drift`

Non-goals:
- Does not change valid recovery replay behavior.
- Does not change pending task creation, cleanup ordering, cleanup execution, lethal-damage runtime behavior, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
