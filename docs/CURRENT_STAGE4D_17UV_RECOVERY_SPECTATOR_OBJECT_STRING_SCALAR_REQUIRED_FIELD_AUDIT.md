# Stage 4D-17UV Recovery Spectator Object String Scalar Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot visible-object string scalar required-field parity gap.

`MatchRecoveryValidator` now treats visible-object string scalars as required when the authoritative visible object carries a non-empty value. Missing, null or empty spectator `cardNo`, `ownerId`, `controllerId` and `attachedToObjectId` payload values emit the explicit required diagnostic and still emit the authoritative object string-scalar mismatch diagnostic. Authoritative-empty values keep existing optional compatibility.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectStringScalarRequiredParity` to mutate a visible battlefield object in the spectator replay-frame snapshot:

- removed `cardNo`
- set `ownerId` to null
- set `controllerId` to an empty string
- removed `attachedToObjectId`

The test proves the validator emits required diagnostics plus authoritative mismatch diagnostics for all four non-empty authoritative string scalars.

## Validation

- Focused visible-object string scalar required parity test: `1/1`
- Focused visible string/scalar adjacent filter: `4/4`
- Focused spectator player object / extra-object filter: `33/33`
- Focused recovery: `678/678`
- Adjacent recovery/opening/store-smoke: `1259/1259`
- Backend full: `6624/6624`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator visible-object string scalar required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
