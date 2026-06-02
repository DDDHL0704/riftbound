# Stage 4D-17UZ Recovery Spectator Extra Visible Object String Scalar Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot extra-visible-object string scalar required-field coverage gap.

`MatchRecoveryValidator` already routes extra objects that are authoritative, spectator-readable, non-hidden and non-face-down through the same visible-object scalar validator used for normal visible objects. This checkpoint locks that behavior with direct coverage for an object visible to another player but incorrectly injected into the current player's spectator snapshot.

Runtime changed: no new production code; recovery frame validation behavior is now covered by regression tests.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraVisibleObjectStringScalarRequiredParityWithVisibilityMismatch` to copy Bob's visible battlefield object payload into Alice's spectator replay-frame snapshot object map, then mutate required string scalars:

- removed `cardNo`
- set `ownerId` to null
- set `controllerId` to an empty string
- removed `attachedToObjectId`

The test proves the validator emits the extra-object visibility mismatch plus required diagnostics and authoritative mismatch diagnostics for all four string scalars.

## Validation

- Focused extra-visible-object string scalar required parity test: `1/1`
- Focused extra-visible-object adjacent filter: `3/3`
- Focused spectator player object / extra-visible-object / extra-object filter: `49/49`
- Focused recovery: `682/682`
- Adjacent recovery/opening/store-smoke broad filter: `1282/1282`
- Backend full: `6628/6628`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator extra-visible-object string scalar required-field regression coverage only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
