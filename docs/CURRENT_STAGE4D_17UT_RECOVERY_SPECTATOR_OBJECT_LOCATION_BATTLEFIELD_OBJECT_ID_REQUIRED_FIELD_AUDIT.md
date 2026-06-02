# Stage 4D-17UT Recovery Spectator Object Location Battlefield Object Id Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot player object-location `battlefieldObjectId` required-field parity gap for authoritative object locations that carry a non-empty battlefield object id.

`MatchRecoveryValidator` now routes visible-object and extra-object `location.battlefieldObjectId` parity through a shared helper. When an expected spectator object location has a non-empty `BattlefieldObjectId`, missing, null or empty spectator `location.battlefieldObjectId` values emit an explicit battlefield object id required diagnostic and the authoritative battlefield-object-id mismatch diagnostic. Malformed present values keep the existing value-shape diagnostic and still emit the mismatch diagnostic when authoritative battlefield-object-id parity applies. Authoritative-empty object locations keep existing optional compatibility.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Updated `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationRequiredFieldParity` to remove a visible object's nested `location.battlefieldObjectId` while authoritative object location expects a battlefield object id. The test now proves the spectator recovery validator emits both:

- `location battlefield object id is required`
- `location battlefield object id does not match authoritative object location battlefield object id`

Updated `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationRequiredFieldParityWithVisibilityMismatch` to set an extra-object nested `location.battlefieldObjectId` to null while authoritative object location expects a battlefield object id. The test now proves the required and authoritative mismatch diagnostics are emitted while preserving the extra-object authoritative spectator visibility mismatch.

## Validation

- Focused location required parity tests: `2/2`
- Focused spectator player object / extra-object location filter: `12/12`
- Focused spectator player object / extra-object filter: `32/32`
- Focused recovery: `676/676`
- Adjacent recovery/opening/store-smoke: `1257/1257`
- Backend full: `6622/6622`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator object-location required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
