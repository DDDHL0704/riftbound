# Stage 4D-17UP Recovery Spectator Extra Object Face Down Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player extra-object redaction payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the missing/null required-field analogue after 17UO: unreadable-present extra-object `isFaceDown` values already emitted authoritative spectator-redaction mismatch diagnostics, but missing or null `isFaceDown` could still stop at the required-field diagnostic in the extra-object redaction path.

## Runtime Change

`MatchRecoveryValidator` now treats an unreadable, missing or null extra-object `isFaceDown` value as a redaction-parity mismatch when the object has authoritative card state or requires spectator face-down redaction.

Readable value drift keeps the existing diagnostic wording. Extra objects that do not correspond to authoritative card state and do not require spectator face-down redaction remain outside this authoritative redaction check.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectRequiredFaceDownParityWithVisibilityMismatch`.

The test forges two hidden hand objects into the spectator snapshot: one omits `isFaceDown`, and one carries `isFaceDown` as null. It asserts both extra objects emit the required-field diagnostic, authoritative spectator-redaction mismatch diagnostic and extra-object visibility mismatch.

## Validation

- Focused extra-object required face-down parity test: `1/1`.
- Focused spectator player object / extra-object filter: `26/26`.
- Focused `MatchRecoveryTests` filter: `670/670`.
- Adjacent recovery/opening/store-smoke filter: `1251/1251`.
- Backend full: `6616/6616`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
