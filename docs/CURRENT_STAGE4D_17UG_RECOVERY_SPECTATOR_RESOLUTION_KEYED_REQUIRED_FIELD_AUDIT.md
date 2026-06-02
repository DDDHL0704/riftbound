# Stage 4D-17UG Recovery Spectator Resolution Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that keyed authoritative resolution diagnostics only ran when same-resolution required scalar/list fields were readable. If the spectator resolution list had a count mismatch, broad ordered parity was skipped; if a same-resolution payload then omitted or made required fields unreadable, the validator emitted generic shape diagnostics but not the keyed authoritative mismatch diagnostics that identify the authoritative same-resolution field drift.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-resolution spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` required fields even when those fields are missing or unreadable.

The helper coverage includes battlefield resolution fields:

- `tick`;
- `kind`;
- `reason`;
- `battlefieldObjectId`;
- `participantObjectIds`;
- `relatedEventKinds`.

The helper coverage includes battle resolution fields:

- `tick`;
- `kind`;
- `reason`;
- `battlefieldId`;
- `attackerObjectIds`;
- `defenderObjectIds`;
- `survivingAttackerObjectIds`;
- `survivingDefenderObjectIds`;
- `destroyedObjectIds`;
- `relatedEventKinds`.

Readable value drift keeps the existing diagnostic wording. Missing/unreadable required fields now also emit keyed authoritative mismatch wording for the matching `resolutionId`.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates spectator replay-frame timing resolution payloads with:

- one authoritative battlefield resolution `battlefield-resolution-1`;
- one authoritative battle resolution `battle-resolution-1`;
- battlefield resolution `tick`, `reason` and `relatedEventKinds` removed from the same-key spectator payload;
- battlefield resolution `kind`, `battlefieldObjectId` and `participantObjectIds` changed to unreadable payload shapes;
- battle resolution `tick`, `reason`, `defenderObjectIds`, `survivingDefenderObjectIds` and `relatedEventKinds` removed from the same-key spectator payload;
- battle resolution `kind`, `battlefieldId`, `attackerObjectIds`, `survivingAttackerObjectIds` and `destroyedObjectIds` changed to unreadable payload shapes;
- one extra battlefield resolution and one extra battle resolution added so both resolution counts mismatch and broad ordered parity stays skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the malformed same-resolution payloads;
- keyed authoritative mismatch diagnostics for the same-resolution fields listed above;
- battlefield-resolution and battle-resolution count mismatches.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistory` filter: `15/15`.
- Focused `ResolutionHistory|Resolution` filter: `80/80`.
- Focused `MatchRecoveryTests` filter: `660/660`.
- Adjacent recovery/opening/store-smoke filter: `1241/1241`.
- Backend full: `6606/6606`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
