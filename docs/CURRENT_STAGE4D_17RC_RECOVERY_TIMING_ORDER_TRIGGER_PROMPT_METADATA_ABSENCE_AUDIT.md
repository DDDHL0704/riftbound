# Stage 4D-17RC Recovery Timing Order-Trigger Prompt Metadata Absence Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovery-frame validation for timing-map separation from `ORDER_TRIGGERS` prompt metadata.

Current runtime builder facts:

- Timing snapshots serialize trigger details under `triggerQueue[]`.
- Order-trigger prompts expose prompt metadata such as ordering player, trigger choices and legal ordering constraints.
- Those prompt metadata fields are not recovery timing fields.

## Runtime Change

`MatchRecoveryValidator` now rejects recovered player-view snapshot timing and spectator replay-frame timing maps when any of these `ORDER_TRIGGERS` prompt-only fields are present:

- `orderingPlayerId`
- `orderedTriggerIds`
- `triggerIds`
- `triggers`
- `triggerChoices`
- `legalOrderingConstraints`
- `triggeredByEventKind`
- `orderingState`

The diagnostic is explicit:

```text
... order-trigger prompt field <field> must be absent
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOrderPromptFieldAbsenceDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDrift`

Both tests inject the full current order-trigger prompt metadata field set into timing and assert that all eight fields are rejected.

## Validation

Passed:

- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter TriggerQueueOrderPromptFieldAbsence --no-restore` (`2/2`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter TriggerQueue --no-restore` (`72/72`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests --no-restore` (`550/550`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests" --no-restore` (`1131/1131`)
- `dotnet test Riftbound.slnx --no-restore` (`6496/6496`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
