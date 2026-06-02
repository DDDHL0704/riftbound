# Stage 4D-17US Recovery Spectator Object Location Payload Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player object location payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the whole-location payload analogue after 17UR: nested visible-object and extra-object `location.playerId` / `location.zone` missing/null values already emitted authoritative object-location mismatch diagnostics, but a missing, null or non-object `location` payload could still stop at required/payload-shape diagnostics.

## Runtime Change

`MatchRecoveryValidator` now treats missing or null visible-object `location` payloads as authoritative object-location mismatches when an expected spectator object location exists.

Visible-object `location` payloads that are present but not object-shaped now emit the same authoritative object-location mismatch diagnostic alongside the existing payload-required diagnostic.

For extra objects, missing, null or non-object `location` payloads now emit the same authoritative object-location mismatch diagnostic when an authoritative spectator object location exists.

Nested readable value drift, unreadable-present drift and nested required-field drift keep the existing diagnostic wording.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationPayloadRequiredParity`.

The visible-object test mutates three spectator-visible battlefield object payloads: one omits `location`, one carries `location` as null, and one carries `location` as a non-object string. It asserts each object emits its required/payload diagnostic plus the authoritative whole-location mismatch diagnostic.

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationPayloadRequiredParityWithVisibilityMismatch`.

The extra-object test forges three hidden hand objects into the spectator snapshot: one omits `location`, one carries `location` as null, and one carries `location` as a non-object string. It asserts required/payload diagnostics, authoritative whole-location mismatch diagnostics and extra-object visibility mismatch coverage.

## Validation

- Focused location payload required parity tests: `2/2`.
- Focused spectator player object / extra-object location filter: `12/12`.
- Focused spectator player object / extra-object filter: `32/32`.
- Focused `MatchRecoveryTests` filter: `676/676`.
- Adjacent recovery/opening/store-smoke filter: `1257/1257`.
- Backend full: `6622/6622`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
