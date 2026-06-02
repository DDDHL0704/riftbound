# Stage 4D-17UI Recovery Spectator Battlefield Task Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `battlefieldTasks[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that same-key authoritative battlefield-task diagnostics compared readable `status`, `actingPlayerId` and `stackItemIds[]` values after 17TB, but missing or unreadable same-key fields could fall back to generic shape diagnostics when battlefield-task count mismatch skipped broad ordered parity. That made the authoritative same-task drift less explicit for the matching `(battlefieldObjectId, kind)`.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-task spectator replay-frame timing `battlefieldTasks[]` fields when those fields are missing or unreadable under task-count mismatch.

The helper coverage includes:

- required `status`;
- authoritative-present `actingPlayerId`;
- required `stackItemIds[]`.

`kind` and `battlefieldObjectId` remain the key fields used to locate the authoritative task; they still must be readable before keyed authoritative value checks can run. Readable value drift keeps the existing diagnostic wording.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates a spectator replay-frame timing active spell-duel battlefield task with:

- one authoritative same-key `START_SPELL_DUEL` task selected by `battlefieldObjectId` and `kind`;
- `status` removed from the same-key spectator payload;
- `actingPlayerId` changed to an unreadable payload shape;
- `stackItemIds[]` changed to an unreadable list payload;
- one extra task added so `battlefieldTasks[]` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the malformed same-key payload;
- keyed authoritative mismatch diagnostics for `status`, `actingPlayerId` and `stackItemIds[]`;
- battlefield-task count mismatch diagnostics.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `BattlefieldTask` filter: `59/59`.
- Focused `MatchRecoveryTests` filter: `662/662`.
- Adjacent recovery/opening/store-smoke filter: `1243/1243`.
- Backend full: `6608/6608`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
