# Stage 4D-17RD Recovery Authoritative Trigger Queue Source Object Completeness Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state trigger queue validation so the server recovery validator rejects trigger queue items whose source object identity was normalized to an empty string.

Current runtime builder facts:

- `TriggerQueueItemState` normalizes null and blank constructor source object ids to an empty string.
- Runtime trigger queue snapshot builders serialize `sourceObjectId` for every trigger queue item.
- Recovered player-view and spectator timing `triggerQueue[]` validators already require a non-empty source object id.

## Runtime Change

`MatchRecoveryValidator` now emits an explicit authoritative-state diagnostic when `TriggerQueueItemState.SourceObjectId` is present as the normalized empty string:

```text
authoritative state trigger queue item <triggerId> source object is required
```

Existing null and whitespace diagnostics are preserved.

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSourceObjectCompletenessDrift`

The test builds an authoritative trigger queue item with `sourceObjectId: null`, exercising the runtime normalization path to an empty string, and asserts that validation rejects the item before snapshot/replay trigger queue consumers can serialize or compare an empty source object id.

## Validation

Passed:

- `dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSourceObjectCompletenessDrift"` (`1/1`)
- `dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`73/73`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`551/551`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1132/1132`)
- `dotnet test Riftbound.slnx --no-restore` (`6497/6497`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
