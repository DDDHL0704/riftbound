# Stage 4D-17UU Recovery Spectator Object Face-Down Required-Field Audit

Date: 2026-06-02

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

This slice closes the spectator replay-frame snapshot player visible-object `isFaceDown` required-field parity gap.

`MatchRecoveryValidator` now routes visible-object and extra-object face-down spectator-redaction parity through a shared helper. Visible-object missing, null or unreadable `isFaceDown` values still emit the existing required or invalid face-down diagnostics, and now consistently also emit the authoritative spectator-redaction mismatch diagnostic when visible-object parity is required. Extra-object behavior remains aligned with the previous authoritative-object / face-down-redaction condition.

Runtime changed: yes, recovery frame validation only.

Protocol shape changed: no.

Frontend, matrix JSON, official catalog, Chrome/browser/formal E2E scripts and `fullOfficial`: unchanged.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectRequiredFaceDownParity` to mutate three authoritative visible objects in the spectator replay-frame snapshot:

- removed `isFaceDown`
- set `isFaceDown` to null
- set `isFaceDown` to a non-bool string

The test proves the validator emits required/invalid face-down diagnostics plus the authoritative spectator-redaction mismatch diagnostic for each visible object.

## Validation

- Focused visible-object face-down required parity test: `1/1`
- Focused face-down adjacent filter: `4/4`
- Focused spectator player object / extra-object filter: `33/33`
- Focused recovery: `677/677`
- Adjacent recovery/opening/store-smoke: `1258/1258`
- Backend full: `6623/6623`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- Touched-file scoped `dotnet format --verify-no-changes --no-restore`: passed

Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`. No unrelated formatting was applied.

## Remaining

This narrows P1-004 replay/recovery determinism and spectator visible-object face-down required-field enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
