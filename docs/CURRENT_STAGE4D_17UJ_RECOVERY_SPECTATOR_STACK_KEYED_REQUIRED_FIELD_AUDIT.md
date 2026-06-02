# Stage 4D-17UJ Recovery Spectator Stack Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot `Stack[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that same-key authoritative stack-item diagnostics compared readable `controllerId`, `sourceObjectId`, `effectKind`, `cardNo`, `targetObjectIds[]`, `damageAmount` and `destination` values after 17TQ, but missing or unreadable same-key fields could fall back to generic shape diagnostics when stack count mismatch skipped broad ordered parity. That made the authoritative same-stack-item drift less explicit for the matching `stackItemId`.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-item spectator replay-frame snapshot `Stack[]` fields when those fields are missing or unreadable under stack-count mismatch.

The helper coverage includes:

- required `controllerId`;
- authoritative-present `sourceObjectId`;
- required `effectKind`;
- authoritative-present `cardNo`;
- required `targetObjectIds[]`;
- required `damageAmount`;
- authoritative-present `destination`.

`stackItemId` remains the key field used to locate the authoritative stack item; it still must be readable before keyed authoritative value checks can run. Readable value drift keeps the existing diagnostic wording.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates a spectator replay-frame snapshot stack item with:

- one authoritative same-key `stack-1` item selected by `stackItemId`;
- required `controllerId` and `effectKind` removed from the same-key spectator payload;
- authoritative-present `sourceObjectId`, `cardNo` and `destination` changed to unreadable payload shapes;
- required `targetObjectIds[]` and `damageAmount` changed to unreadable payload shapes;
- one extra stack item added so `Stack[]` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the malformed same-key payload;
- keyed authoritative mismatch diagnostics for `controllerId`, `sourceObjectId`, `effectKind`, `cardNo`, `targetObjectIds[]`, `damageAmount` and `destination`;
- extra stack-item id and stack-count mismatch diagnostics.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `SpectatorReplaySnapshotStack` filter: `18/18`.
- Focused `MatchRecoveryTests` filter: `663/663`.
- Adjacent recovery/opening/store-smoke filter: `1244/1244`.
- Backend full: `6609/6609`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
