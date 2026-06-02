# Stage 4D-17UH Recovery Spectator Pending Task Queue Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `pendingTaskQueue.tasks[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that keyed authoritative pending-task diagnostics only ran when same-task required fields were readable. If the spectator pending-task list had a task-count mismatch, broad ordered parity was skipped; if a same-task payload then omitted or made required/authoritative-present fields unreadable, the validator emitted generic shape diagnostics but not all keyed authoritative mismatch diagnostics that identify the authoritative same-task field drift.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-task spectator replay-frame timing `pendingTaskQueue.tasks[]` required and authoritative-present fields when those fields are missing or unreadable under task-count mismatch.

The helper coverage includes:

- required `kind`;
- required `reason`;
- authoritative-present `playerId`;
- authoritative-present `battlefieldObjectId`;
- authoritative-present visible `objectId`.

Readable value drift keeps the existing diagnostic wording. Missing/unreadable required fields now also emit keyed authoritative mismatch wording for the matching `taskId`. Optional fields still allow missing/empty payloads when authoritative state has no value; when authoritative state has a value, unreadable payloads now also emit keyed mismatch diagnostics.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueKeyedRequiredFieldAbsenceWithTaskCountMismatch`.

The test mutates a spectator replay-frame timing pending-task payload with:

- one visible authoritative equipment cleanup task selected by `objectId`;
- `kind` removed from the same-task spectator payload;
- `reason`, `playerId`, `battlefieldObjectId` and `objectId` changed to unreadable payload shapes;
- one extra pending task added so `pendingTaskQueue.tasks[]` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the malformed same-task payload;
- keyed authoritative mismatch diagnostics for the same-task fields listed above;
- extra forged task id and pending-task count mismatch diagnostics.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `PendingTaskQueue` filter: `35/35`.
- Focused `MatchRecoveryTests` filter: `661/661`.
- Adjacent recovery/opening/store-smoke filter: `1242/1242`.
- Backend full: `6607/6607`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
