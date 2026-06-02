# Stage 4D-17UO Recovery Spectator Extra Object Face Down Unreadable Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player extra-object redaction payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the extra-object `isFaceDown` analogue after 17UN: readable extra-object face-down drift already emitted authoritative spectator-redaction mismatch diagnostics. Present but unreadable `isFaceDown` values could still stop at required/shape diagnostics in the extra-object redaction path.

## Runtime Change

`MatchRecoveryValidator` now records whether an extra-object `isFaceDown` payload field is present and non-null before parsing it. When an extra object has an authoritative card object or requires spectator face-down redaction, unreadable-present `isFaceDown` now emits the authoritative spectator-redaction mismatch diagnostic alongside the existing required/shape diagnostic.

Readable value drift keeps the existing diagnostic wording. Missing/null `isFaceDown` behavior is unchanged.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectUnreadableFaceDownParityWithVisibilityMismatch`.

The test forges a hidden hand object into the spectator snapshot with a valid `objectId` and an unreadable-present `isFaceDown` value, then asserts the face-down shape diagnostic, authoritative spectator-redaction mismatch diagnostic and extra-object visibility mismatch all remain present.

## Validation

- Focused extra-object unreadable face-down parity test: `1/1`.
- Focused spectator player object / extra-object filter: `25/25`.
- Focused `MatchRecoveryTests` filter: `669/669`.
- Adjacent recovery/opening/store-smoke filter: `1250/1250`.
- Backend full: `6615/6615`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
