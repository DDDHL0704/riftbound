# Stage 4D-17UX Recovery Spectator Object Boolean Scalar Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot visible-object boolean scalar required-field parity gap.

`MatchRecoveryValidator` now treats visible-object boolean scalars as required when the authoritative visible object is serialized with those values. Missing or null spectator `isExhausted`, `isAttacking` and `isDefending` payload values emit the explicit required diagnostic and still emit the authoritative object boolean-scalar mismatch diagnostic.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectBooleanScalarRequiredParity` to mutate a visible battlefield object in the spectator replay-frame snapshot:

- removed `isExhausted`
- set `isAttacking` to null
- removed `isDefending`

The test proves the validator emits required diagnostics plus authoritative mismatch diagnostics for all three visible-object boolean combat/status scalars. Visible-object `isFaceDown` stays on the separate spectator-redaction parity path covered by Stage 4D-17UU.

## Validation

- Focused visible-object boolean scalar required parity test: `1/1`
- Focused visible-object adjacent filter: `12/12`
- Focused spectator player object / extra-object filter: `45/45`
- Focused recovery: `680/680`
- Adjacent recovery/opening/store-smoke: `1261/1261`
- Backend full: `6626/6626`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator visible-object boolean scalar required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
