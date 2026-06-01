# Stage 4D-17RB Recovery Timing Trigger-Queue Prompt-Field Absence Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovery-frame validation for trigger-queue timing payload shape.

Current runtime builder facts:

- Timing snapshots serialize trigger queue items with `triggerId`, `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind`.
- Trigger ordering prompt views may expose user-facing `summary` and `visibleText`.
- `summary` and `visibleText` are prompt fields, not recovery timing fields.

## Runtime Change

`MatchRecoveryValidator` now rejects recovered player-view snapshot timing and spectator replay-frame timing `triggerQueue[]` item payloads when either of these prompt-only fields is present:

- `summary`
- `visibleText`

The diagnostics are explicit:

```text
... summary must be absent from timing trigger queue item
... visible text must be absent from timing trigger queue item
```

The check runs before spectator authoritative trigger-queue parity can skip same-payload validation because of a count mismatch.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueuePromptFieldAbsenceDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePromptFieldAbsenceDrift`

The spectator test keeps the same count-mismatch path used by adjacent recovery slices: authoritative trigger queue is empty, the spectator frame carries one malformed trigger, and same-payload prompt-field absence validation still emits diagnostics alongside the count mismatch.

## Validation

Passed:

- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter TriggerQueuePromptFieldAbsence --no-restore` (`2/2`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter TriggerQueue --no-restore` (`70/70`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests --no-restore` (`548/548`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests" --no-restore` (`1129/1129`)
- `dotnet test Riftbound.slnx --no-restore` (`6494/6494`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
