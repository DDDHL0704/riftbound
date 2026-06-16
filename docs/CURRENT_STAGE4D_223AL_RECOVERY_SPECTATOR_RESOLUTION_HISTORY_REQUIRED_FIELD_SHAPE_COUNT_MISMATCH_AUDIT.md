# Stage 4D-223AL Recovery Spectator Resolution History Required-Field Shape Count-Mismatch Audit

Date: 2026-06-16 13:07 CST

Status: accepted on local `main` as code commit `6a27986d`; docs checkpoint follows this audit. Project remains **NOT READY**.

## Scope

- Server validation slice: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.

## Coverage

`MatchRecoveryTests` now adds `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKeyedRequiredFieldShapeWithCountMismatch`.

The new test keeps spectator `battlefieldResolutions[]` and `battleResolutions[]` entries keyed by their authoritative `resolutionId` while corrupting required scalar/list payload shapes and appending an extra retained resolution. This proves the recovery validator reports both count drift and per-entry required-field shape diagnostics for battlefield-resolution and battle-resolution history, instead of losing keyed validation when ordered parity is already invalid.

## Rule Source

Rule source checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`:

- Latest core rules 454-461 for battle identity, battle steps, combat damage, cleanup, result determination and battle end.
- Latest core rules 460.2.c and 460.2.d for damage assignment before simultaneous damage application.
- Latest core rules 461.3, 461.5 and 461.7 for battle result, battlefield control establishment and battle identity cleanup.
- Latest core rules 463-467 plus 323.1 for battlefield scoring, conquest/hold scoring, winning-score checks and cleanup win checks.

## Validation

- Focused new test: `1/1` passed.
- Changed class: `MatchRecoveryTests` `1940/1940` passed.
- Adjacent ResolutionHistory / SpectatorReplayTiming / BattlefieldResolution / BattleResolution / BattleDamageAssignment filter: `1314/1314` passed.
- Backend full: `8270/8270` passed.
- `git diff --check`: passed before code commit.
- Anchored conflict-marker scan over the changed test file: passed before code commit.

## Coordination

- A_MAIN continued directly on `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` branch `main` per user request.
- Other-window branch review after fetch found no commits ahead of current `main` for `codex/local-2p-smoke-20260612`, `codex/rule-audit-local2p-20260615`, `codex/rule-audit-local2p-worktree-20260615` or `codex/rule-audit-remaining-20260615`.
- `rule-audit-remaining-20260615` remains behind the pushed main history and had no ahead commits to merge.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`.
- No subagent was created.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, official catalog, Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Open

This narrows recovery spectator replay timing resolution-history required-field shape coverage under count mismatch only. It does not change recovery runtime behavior, battle declaration legality, combat assignment, simultaneous damage, cleanup, scoring, match-finished enforcement, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness. Project remains **NOT READY**.
