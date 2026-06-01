# Stage 4D-17RK Recovery Timing Trigger Queue Source Registry Key Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovered player-view and spectator replay-frame timing trigger queue source-object membership so visible `triggerQueue[]` source ids are checked against canonical object registry keys.

Current runtime builder facts:

- Recovered snapshot player `objects{}` maps identify objects by their map keys; the nested `objectId` field is separately validated against that key.
- Authoritative state object registries identify objects by `CardObjects` and `ObjectLocations` map keys; `CardObjectState.ObjectId` is separately validated against its map key.
- A mismatched nested/self-declared object id must not be allowed to satisfy timing `triggerQueue[]` visible source-object membership.
- Hidden trigger sources continue to use `sourceVisibility: "HIDDEN"` with redacted `sourceObjectId` / `effectKind`.

## Runtime Change

`MatchRecoveryValidator` now builds trigger-queue known-object sets from canonical registry keys only:

- recovered snapshot known objects use snapshot player `objects{}` map keys;
- spectator/authoritative known objects use authoritative `CardObjects` and `ObjectLocations` keys.

Visible trigger queue source objects now emit the existing missing-object diagnostics when the only matching id is a mismatched nested/self-declared object id:

```text
snapshot for <playerId> timing trigger queue item visible source object id <sourceObjectId> is missing from objects
spectator replay frame timing trigger queue item visible source object id <sourceObjectId> is missing from object registry
```

Existing object identity mismatch diagnostics are preserved and now appear alongside the trigger-queue membership diagnostic when both facts are present.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectPayloadIdentityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectPayloadIdentityDrift`

The recovered test seeds a snapshot object map keyed by `actual-source` whose nested `objectId` incorrectly says `source-1`, then mutates timing `triggerQueue[]` to claim visible `source-1`. The spectator test seeds authoritative `CardObjects`/`ObjectLocations` under `actual-source` while the `CardObjectState.ObjectId` incorrectly says `source-1`, then adds a mismatched spectator trigger queue item that claims visible `source-1`. Both tests prove the identity mismatch no longer masks the missing canonical source-object membership diagnostic.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectPayloadIdentityDrift|RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectPayloadIdentityDrift"` (`2/2`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`85/85`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`563/563`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1144/1144`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6509/6509`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
