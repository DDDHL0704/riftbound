2026-06-16 17:46 CST

Stage 4D-223AV recovery spectator trigger-queue null-payload count-drift validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.
- Touched code: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueNullPayloadWithCountMismatch`.
- The test builds a non-empty authoritative visible-source `triggerQueue[]`, sets spectator replay timing `triggerQueue` to `null`, and proves both the required-payload error and count `0` versus authoritative count `1` are emitted.
- The existing `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueNullPayloadWithoutCountMismatch` remains green, proving empty-authoritative null payload still omits count mismatch.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 327 and 333 for stack / chain creation context.
- 342, 376 and 382-383 for spell duel, active skills and triggered skill ordering / placement.
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
- Existing null without-count companion: `1/1`.
- Changed-class `MatchRecoveryTests`: `1950/1950`.
- Adjacent TriggerQueue/SpectatorReplayTiming filter: `1470/1470`.
- Backend full: `8280/8280`.
- `git diff --check` passed before code commit.
- Changed-test anchored conflict-marker scan passed before code commit.

Code commit:
- `528b5aa9 test: cover recovery trigger queue null payload count drift`

Non-goals:
- Does not change recovery runtime behavior beyond the already accepted 4D-223AU diagnostic.
- Does not change trigger ordering, stack placement, Last Breath runtime behavior, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
