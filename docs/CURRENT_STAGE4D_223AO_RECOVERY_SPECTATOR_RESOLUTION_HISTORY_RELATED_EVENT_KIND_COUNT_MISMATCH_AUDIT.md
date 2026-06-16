2026-06-16 13:41 CST

Stage 4D-223AO recovery spectator resolution-history related-event-kind count-mismatch validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.
- Touched code: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRelatedEventKindValueDriftWithCountMismatch`.
- The test builds an authoritative retained `battlefieldResolutions[]` and `battleResolutions[]` state, then corrupts spectator replay timing `relatedEventKinds[]` with unknown event-kind values while appending an extra retained resolution payload.
- The test proves keyed validation by authoritative `resolutionId` still emits related-event-kind known-value diagnostics while count mismatch and the extra missing authoritative resolution id are also reported.
- Covered battlefield-resolution related event kind: `UNKNOWN_BATTLEFIELD_EVENT`.
- Covered battle-resolution related event kind: `UNKNOWN_BATTLE_EVENT`.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 454-461 for battle identity, battle steps, combat damage, cleanup, result and battle end.
- 460.2.c/460.2.d for damage assignment before simultaneous damage.
- 461.3/461.5/461.7 for battle result, control and battle-end cleanup.
- 463-467 and 323.1 for scoring and cleanup win checks.

Coordination:
- No subagent was created.
- A_MAIN continued directly on local `main` per user request.
- Other-window branch review after fetch found no commits ahead of current `main` for `codex/local-2p-smoke-20260612`, `codex/rule-audit-local2p-20260615`, `codex/rule-audit-local2p-worktree-20260615` or `codex/rule-audit-remaining-20260615`.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`, observed 2026-06-16 13:41 CST.

Validation passed:
- Focused new test: `1/1`.
- Changed-class `MatchRecoveryTests`: `1943/1943`.
- Adjacent ResolutionHistory/SpectatorReplayTiming/BattlefieldResolution/BattleResolution/BattleDamageAssignment filter: `1317/1317`.
- Backend full: `8273/8273`.
- `git diff --check` passed before code commit.
- Changed-test anchored conflict-marker scan passed before code commit.

Code commit:
- `d68e8aa1 test: cover recovery resolution history related events count mismatch`

Non-goals:
- Does not change recovery runtime behavior.
- Does not change battle declaration legality, combat assignment, simultaneous damage, cleanup, scoring or match-finished enforcement.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.
