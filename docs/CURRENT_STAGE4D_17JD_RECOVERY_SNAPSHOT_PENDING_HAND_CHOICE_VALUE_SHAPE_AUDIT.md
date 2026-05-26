# Stage 4D-17JD Recovery Snapshot Pending Hand-Choice Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 10:16 CST. Project remains **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, or final readiness status.

`MatchRecoveryValidator.ValidateSnapshotShape` now validates present `Timing["pendingHandChoice"]` scalar/count/state/list values before pending-hand-choice field reads and parity checks consume that payload.

Covered required string fields:

- `choiceId`
- `choiceWindow`
- `playerId`

Covered required count fields:

- `requiredCount`
- `maxCount`

Covered optional-present string fields:

- `reason`
- `sourceObjectId`
- `effectKind`

Covered state/list fields:

- `choiceState`
- `legalObjectIds`

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now calls `ValidateSnapshotTimingPendingHandChoicePayloadValues` from snapshot shape validation. The snapshot helper uses shared `ValidatePendingHandChoicePayloadValues` logic with recovered-snapshot labels, allows the two recovered player-view choice states (`PENDING_CHOICE` and `WAITING_FOR_CHOICE`), and validates optional-present `legalObjectIds` list values.

The existing spectator pending-hand-choice value validation now delegates to the same shared scalar/count/state helper while retaining its stricter spectator state rule (`WAITING_FOR_CHOICE`) and existing legal-object redaction validation.

The recovered snapshot validation rejects malformed pending hand-choice drift:

- required strings must be present, non-blank, string typed and free of surrounding whitespace;
- optional-present strings must be string typed, non-blank and free of surrounding whitespace when provided;
- required counts must be present, non-null, integer typed and positive;
- `maxCount` cannot be less than `requiredCount`;
- `choiceState` must be `PENDING_CHOICE` or `WAITING_FOR_CHOICE`;
- optional-present `legalObjectIds` must be a string list without blank, surrounding-whitespace or duplicate normalized entries.

## Test Coverage

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` adds `RecoveryValidatorRejectsSnapshotTimingPendingHandChoiceValueDrift`.

The test mutates recovered player-view snapshot `Timing["pendingHandChoice"]` with invalid required strings, malformed required/optional values, non-positive counts, an unknown choice state and malformed legal-object lists, then asserts explicit recovered snapshot diagnostics.

## Validation

- Focused single: `RecoveryValidatorRejectsSnapshotTimingPendingHandChoiceValueDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `283/283`.
- Adjacent recovery/opening/store-smoke filter passed `864/864`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `6229/6229`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over docs/src/tests, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered snapshot pending hand-choice value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
