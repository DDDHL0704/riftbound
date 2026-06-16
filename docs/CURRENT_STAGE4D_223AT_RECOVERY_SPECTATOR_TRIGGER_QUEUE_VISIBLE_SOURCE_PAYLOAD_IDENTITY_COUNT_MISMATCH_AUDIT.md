2026-06-16 15:41 CST

Stage 4D-223AT recovery spectator trigger-queue visible-source payload identity count-mismatch validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.
- Touched code: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectPayloadIdentityDriftWithCountMismatch`.
- The test builds an authoritative visible-source `triggerQueue[]` where the card-object payload object id disagrees with its registry key, then appends an extra spectator trigger.
- The test proves authoritative card-object map-key/object-id diagnostics, authoritative trigger-source registry absence, spectator visible source-object registry absence, extra spectator trigger-id diagnostics and trigger-queue count mismatch all survive together.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 128 and 129.3 for private/hidden information boundaries and object visibility context.
- 160-166 for object/card identity context.
- 327 and 333 for stack / chain creation context.
- 401-404 for active / triggered skill placement, choices, cost determination and payment.
- 808.1.d for Last Breath pending-item creation and source snapshot context.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before the slice and before code commit; it was clean at `01364ee2` with no commits ahead of `main`.
- `rule-audit-remaining-20260615` had no new commits ahead of `main` before code commit.
- Root PDF rule files remained present.

Validation passed:
- Focused new test: `1/1`.
- Changed-class `MatchRecoveryTests`: `1948/1948`.
- Adjacent TriggerQueue/SpectatorReplayTiming filter: `1468/1468`.
- Backend full: `8278/8278`.
- `git diff --check` passed before code commit.
- Changed-test anchored conflict-marker scan passed before code commit.

Code commit:
- `6d96a6be test: cover recovery trigger queue visible source identity count drift`

Non-goals:
- Does not change recovery runtime behavior.
- Does not change trigger ordering, stack placement, Last Breath runtime behavior, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
