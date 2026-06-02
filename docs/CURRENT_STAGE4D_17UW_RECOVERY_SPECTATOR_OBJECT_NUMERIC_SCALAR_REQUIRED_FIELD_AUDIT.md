# Stage 4D-17UW Recovery Spectator Object Numeric Scalar Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot visible-object numeric scalar required-field parity gap.

`MatchRecoveryValidator` now treats visible-object numeric scalars as required when the authoritative visible object is serialized with those values. Missing or null spectator `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier` and `manaCost` payload values emit the explicit required diagnostic and still emit the authoritative object numeric-scalar mismatch diagnostic.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectNumericScalarRequiredParity` to mutate a visible battlefield object in the spectator replay-frame snapshot:

- removed `damage`
- set `power` to null
- removed `basePower`
- set `effectivePower` to null
- removed `untilEndOfTurnPowerModifier`
- set `manaCost` to null

The test proves the validator emits required diagnostics plus authoritative mismatch diagnostics for all six visible-object numeric scalars.

## Validation

- Focused visible-object numeric scalar required parity test: `1/1`
- Focused visible-object adjacent filter: `11/11`
- Focused spectator player object / extra-object filter: `44/44`
- Focused recovery: `679/679`
- Adjacent recovery/opening/store-smoke: `1260/1260`
- Backend full: `6625/6625`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator visible-object numeric scalar required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
