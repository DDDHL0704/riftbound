# Stage 4D-17UY Recovery Spectator Object List Scalar Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot visible-object list scalar required-field parity gap.

`MatchRecoveryValidator` now treats visible-object list scalars as required when the authoritative visible object is serialized with those values. Missing or null spectator `tags` and `untilEndOfTurnEffects` payload values emit explicit required diagnostics and still emit the authoritative object list-scalar mismatch diagnostics.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectListScalarRequiredParity` to mutate a visible battlefield object in the spectator replay-frame snapshot:

- removed `tags`
- set `untilEndOfTurnEffects` to null

The test proves the validator emits required diagnostics plus authoritative mismatch diagnostics for both visible-object list scalars. Existing list payload-shape and value-shape diagnostics remain covered by the adjacent list scalar shape tests.

## Validation

- Focused visible-object list scalar required parity test: `1/1`
- Focused visible-object adjacent filter: `13/13`
- Focused spectator player object / extra-object filter: `46/46`
- Focused recovery: `681/681`
- Adjacent recovery/opening/store-smoke: `1262/1262`
- Backend full: `6627/6627`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator visible-object list scalar required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
