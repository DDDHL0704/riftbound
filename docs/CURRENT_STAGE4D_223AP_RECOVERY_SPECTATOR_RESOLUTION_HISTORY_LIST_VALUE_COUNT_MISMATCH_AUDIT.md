2026-06-16 14:09 CST

Stage 4D-223AP recovery spectator resolution-history list-value count-mismatch validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.
- Touched code: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryListValueDriftWithCountMismatch`.
- The test builds an authoritative retained `battlefieldResolutions[]` and `battleResolutions[]` state, then corrupts spectator replay timing list values while appending an extra retained resolution payload.
- The test proves list-value validation still emits diagnostics for surrounding whitespace, duplicate values, missing string values and non-string list elements while count mismatch is also reported.
- Covered battlefield-resolution lists: `participantObjectIds[]` and `relatedEventKinds[]`.
- Covered battle-resolution lists: `attackerObjectIds[]`, `defenderObjectIds[]`, `survivingAttackerObjectIds[]`, `survivingDefenderObjectIds[]`, `destroyedObjectIds[]` and `relatedEventKinds[]`.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 454-461 for battle identity, battle steps, combat damage, cleanup, result and battle end.
- 460.2.c/460.2.d for damage assignment before simultaneous damage.
- 461.3/461.5/461.7 for battle result, control and battle-end cleanup.
- 463-467 and 323.1 for scoring and cleanup win checks.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before the slice and before code commit; it was clean at `01364ee2` with no commits ahead of `main`.
- Root PDF rule files remained present.

Validation passed:
- Focused new test: `1/1`.
- Changed-class `MatchRecoveryTests`: `1944/1944`.
- Adjacent ResolutionHistory/SpectatorReplayTiming/BattlefieldResolution/BattleResolution/BattleDamageAssignment filter: `1318/1318`.
- Backend full: `8274/8274`.
- `git diff --check` passed before code commit.
- Changed-test anchored conflict-marker scan passed before code commit.

Code commit:
- `a44fbc79 test: cover recovery resolution history list values count mismatch`

Non-goals:
- Does not change recovery runtime behavior.
- Does not change battle declaration legality, combat assignment, simultaneous damage, cleanup, scoring or match-finished enforcement.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
